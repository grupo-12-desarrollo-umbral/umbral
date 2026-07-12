using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using umbral_backend.Api.Hubs;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Messaging;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

/// <summary>
/// HU-33B X.4 end-to-end gate: a real facade-driven trivia round close both (a) publishes the
/// SessionResultsFinalizedIntegrationEvent on its MassTransit exchange when the close finishes the
/// session via SessionCompletion,
/// AND (b) still fires the HU-33A/21A SignalR broadcasts (QuestionClosed + SessionStateChanged→Finished)
/// to live-session:{id}. Proves the MassTransit producer rides alongside the runtime without
/// regressing the real-time transport. Skipped cleanly when Docker/Testcontainers is unavailable.
/// </summary>
[Collection(PostgreSqlCollection.Name)]
public sealed class RoundClosePublicationEndToEndTests : IAsyncLifetime
{
    private const int OperatorUserId = 71;
    private const string OperatorExternalIdentityId = "kc-operator-71";
    private static readonly JsonSerializerOptions MassTransitJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly PostgreSqlFixture _fixture;
    private RabbitMqContainer? _rabbit;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private bool _brokerAvailable;

    public RoundClosePublicationEndToEndTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Pin guest/guest to match the publisher's production defaults (the module otherwise
        // defaults to rabbitmq/rabbitmq → ACCESS_REFUSED). Mirrors the X.3 integration test.
        _rabbit = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();

        await DockerAvailability.StartOrSkipAsync(() => _rabbit.StartAsync(), _rabbit.DisposeAsync);
        _brokerAvailable = true;

        // MassTransit binds its RabbitMQ host options from configuration; env vars point the booted app at the
        // Testcontainers broker (WebApplication.CreateBuilder reads env vars by default).
        Environment.SetEnvironmentVariable("RabbitMq__HostName", _rabbit.Hostname);
        Environment.SetEnvironmentVariable(
            "RabbitMq__Port", _rabbit.GetMappedPublicPort(5672).ToString());

        _factory = new SessionOperationsApiWebApplicationFactory(
            _fixture.ConnectionString,
            useRealPublishEndpoint: true);
        _factory.AccessClient.IsAllowed = true;
        _factory.AuthenticatedActorProfileAccessClient.CurrentActor = new AuthenticatedActorProfileLookupDto(
            OperatorUserId,
            OperatorExternalIdentityId,
            "Operator",
            true);
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        if (_brokerAvailable)
        {
            await _factory.DisposeAsync();
        }

        Environment.SetEnvironmentVariable("RabbitMq__HostName", null);
        Environment.SetEnvironmentVariable("RabbitMq__Port", null);

        if (_rabbit is not null)
        {
            await _rabbit.DisposeAsync();
        }
    }

    [Fact]
    public async Task ClosingLastQuestionToFinished_PublishesSessionResultsFinalizedAndSignalRBroadcasts()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedActiveSingleQuestionTriviaSessionAsync(externalIdentityId);

        // Bind before the close so the published event cannot be dropped before the test queue exists.
        await using var consumerConnection = await CreateBrokerConnectionAsync();
        await using var consumerChannel = await consumerConnection.CreateChannelAsync();
        await consumerChannel.ExchangeDeclareAsync(
            "session-results-finalized", ExchangeType.Fanout, durable: true, autoDelete: false);
        var finalizedQueue = await BindQueueAsync(consumerChannel, "session-results-finalized");

        // Join the live-session group so the participant is subscribed to the HU-33A/21A broadcasts.
        await using var participant = CreateHubConnection(
            externalIdentityId.ToString(), "Participant", "participant@example.com");
        var closedBroadcast = new TaskCompletionSource<QuestionClosedNotificationDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stateBroadcast = new TaskCompletionSource<SessionStateChangedNotificationDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        participant.On<QuestionClosedNotificationDto>(
            SignalRSessionQuestionBroadcaster.QuestionClosedMethod,
            notification => closedBroadcast.TrySetResult(notification));
        participant.On<SessionStateChangedNotificationDto>(
            SessionStateBroadcaster.StateChangedMethod,
            notification =>
            {
                if (notification.CurrentState == nameof(SessionState.Finished))
                {
                    stateBroadcast.TrySetResult(notification);
                }
            });

        await participant.StartAsync();
        await participant.InvokeAsync(
            nameof(SessionsHub.ReconnectAsync),
            seeded.LiveSessionId,
            new SessionsHub.ReconnectParticipantHubRequest(seeded.TeamId, "Nova", null));

        // Drive the authoritative round exactly as the timer worker does on last-question expiry:
        // the facade closes the only question, advances past the only substage, and — no substage
        // remaining — completes the session via SessionCompletion → Finished. The SaveChanges
        // dispatches run both MassTransit publish handlers; the finalized event is asserted below.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
            var facade = scope.ServiceProvider.GetRequiredService<ITriviaRoundOrchestratorFacade>();
            var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
            await facade.CloseAndAdvanceAsync(session!, DateTimeOffset.UtcNow, CancellationToken.None);
        }

        // (a) SignalR: HU-33A QuestionClosed + HU-21A SessionStateChanged(→Finished) still reach the group.
        var closedNotification = await AwaitBroadcast(closedBroadcast.Task);
        closedNotification.LiveSessionId.Should().Be(seeded.LiveSessionId);
        closedNotification.QuestionIndex.Should().Be(0);

        var stateNotification = await AwaitBroadcast(stateBroadcast.Task);
        stateNotification.LiveSessionId.Should().Be(seeded.LiveSessionId);
        stateNotification.CurrentState.Should().Be(nameof(SessionState.Finished));

        // (b) RabbitMQ: exactly one MassTransit-enveloped SessionResultsFinalized integration event
        // carrying the session correlation field.
        var finalized = await DrainSingleMassTransitMessageAsync<SessionResultsFinalizedIntegrationEvent>(
            consumerChannel, finalizedQueue);
        finalized.LiveSessionId.Should().Be(seeded.LiveSessionId);
    }

    private async Task<IConnection> CreateBrokerConnectionAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbit!.Hostname,
            Port = _rabbit.GetMappedPublicPort(5672),
            UserName = "guest",
            Password = "guest",
        };
        return await factory.CreateConnectionAsync();
    }

    private static async Task<string> BindQueueAsync(IChannel channel, string exchangeName)
    {
        var queueName = $"e2e.{exchangeName}.{Guid.NewGuid():N}";
        await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(queueName, exchangeName, routingKey: string.Empty);
        return queueName;
    }

    private static async Task<T> DrainSingleMassTransitMessageAsync<T>(IChannel channel, string queueName)
    {
        var messages = new List<T>();
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var delivery = await channel.BasicGetAsync(queueName, autoAck: true);
            if (delivery is not null)
            {
                using var envelope = JsonDocument.Parse(delivery.Body);
                messages.Add(envelope.RootElement
                    .GetProperty("message")
                    .Deserialize<T>(MassTransitJsonOptions)!);
            }
            else if (messages.Count > 0)
            {
                break; // first message arrived and the queue has since drained empty.
            }
            else
            {
                await Task.Delay(200);
            }
        }

        messages.Should().ContainSingle(
            "the close must publish exactly one {0} through MassTransit", typeof(T).Name);
        return messages[0];
    }

    private static async Task<T> AwaitBroadcast<T>(Task<T> received)
    {
        var completed = await Task.WhenAny(received, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(received, "the close must broadcast over SignalR (HU-33A/21A not regressed)");
        return await received;
    }

    private HubConnection CreateHubConnection(string userId, string role, string email)
    {
        return new HubConnectionBuilder()
            .WithUrl(
                new Uri(_factory.Server.BaseAddress, "/hubs/sessions"),
                options =>
                {
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Headers["X-User-Id"] = userId;
                    options.Headers["X-User-Role"] = role;
                    options.Headers["X-User-Email"] = email;
                })
            .Build();
    }

    // A single-substage, single-question trivia mission in Active with its only question already
    // activated — closing it exhausts the substage and, with no substage remaining, finishes the session.
    private async Task<SeededSession> SeedActiveSingleQuestionTriviaSessionAsync(Guid externalIdentityId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);

        var sourceMissionId = Guid.NewGuid();
        var substage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);
        var snapshot = MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Single Question Trivia Mission",
            MaximumTime.Create(20),
            [
                StageSnapshot.Create("Stage One", 1, [substage])
            ],
            [],
            [
                TriviaQuestionSnapshot.Create(
                    substage.SubstageSnapshotId,
                    "Capital of France?",
                    1,
                    50,
                    30,
                    "Paris is the capital city.",
                    [
                        TriviaOptionSnapshot.Create("Paris", 1, true),
                        TriviaOptionSnapshot.Create("Lyon", 2, false)
                    ])
            ]);

        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Single Question Trivia Session",
            20,
            createdAt,
            snapshot);

        var team = session.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);
        session.AssignOperator(OperatorUserId, createdAt.AddMinutes(1));
        var participant = session.AdmitParticipant(
            externalIdentityId,
            "Nova",
            team.TeamId,
            createdAt.AddMinutes(1),
            new JoinPolicy()).Participant;
        session.DisconnectParticipant(participant.SessionParticipantId, createdAt.AddMinutes(5));

        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(2), transitionPolicy);
        session.MoveTo(SessionState.Active, createdAt.AddMinutes(3), transitionPolicy);
        // Activate NOW so the 30s question timer is live: the background AuthoritativeSessionTimerWorker
        // (1s tick) must not auto-close/finish this session before the test drives the close itself.
        session.ActivateQuestion(0, DateTimeOffset.UtcNow);

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(session.LiveSessionId, team.TeamId);
    }

    private sealed record SeededSession(Guid LiveSessionId, Guid TeamId);
}
