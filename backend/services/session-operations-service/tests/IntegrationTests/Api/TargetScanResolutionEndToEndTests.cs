using System.Text.Json;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

/// <summary>
/// HU-31 X.4 end-to-end gate. Driving the participant POST /participants/target-scans endpoint against a
/// real RabbitMQ broker:
///   (a) a CORRECT scan resolves the target and publishes BOTH ordered facts — the umbrella
///       EvidenceSubmissionRegistered (audit/history) and the TargetResolved fact (scoring) — after the
///       transaction commits, with TargetResolved keyed to the same EvidenceSubmissionId;
///   (b) a REJECTED scan (wrong QR) still registers and publishes EvidenceSubmissionRegistered but NEVER
///       TargetResolved.
/// The endpoint's 200 body never leaks the score — it travels only on the TargetResolved contract.
/// Skipped cleanly when Docker/Testcontainers is unavailable.
/// </summary>
[Collection(PostgreSqlCollection.Name)]
public sealed class TargetScanResolutionEndToEndTests : IAsyncLifetime
{
    private const string CorrectQr = "QR-ALPHA";
    private const int TargetScore = 100;
    private static readonly JsonSerializerOptions MassTransitJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly PostgreSqlFixture _fixture;
    private RabbitMqContainer? _rabbit;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private bool _brokerAvailable;

    public TargetScanResolutionEndToEndTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Pin guest/guest to the publisher's production defaults (module otherwise defaults to
        // rabbitmq/rabbitmq -> ACCESS_REFUSED). Mirrors RoundClosePublicationEndToEndTests.
        _rabbit = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();

        await DockerAvailability.StartOrSkipAsync(() => _rabbit.StartAsync(), _rabbit.DisposeAsync);
        _brokerAvailable = true;

        // MassTransit binds its RabbitMQ host from configuration; env vars point the booted app at the
        // Testcontainers broker (WebApplication.CreateBuilder reads env vars by default).
        Environment.SetEnvironmentVariable("RabbitMq__HostName", _rabbit.Hostname);
        Environment.SetEnvironmentVariable("RabbitMq__Port", _rabbit.GetMappedPublicPort(5672).ToString());

        _factory = new SessionOperationsApiWebApplicationFactory(
            _fixture.ConnectionString,
            useRealPublishEndpoint: true);
        _client = _factory.CreateClient();
        _factory.EligibleTeamsClient.IsEligible = true;
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        if (_brokerAvailable)
        {
            _client.Dispose();
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
    public async Task CorrectScan_PublishesEvidenceSubmissionRegisteredThenTargetResolved_EndToEnd()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        AddTrustedHeaders(seeded.ParticipantExternalIdentityId.ToString());

        // Bind before the scan so the fanout facts cannot be dropped before the queues exist.
        await using var connection = await CreateBrokerConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var evidenceQueue = await DeclareAndBindAsync(channel, "session-evidence-submission-registered");
        var targetResolvedQueue = await DeclareAndBindAsync(channel, "session-target-resolved");

        var response = await _client.PostAsJsonAsync(BuildScansUrl(seeded), Scan(seeded, CorrectQr));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<RegisterTargetScanResponse>();
        payload!.IsResolved.Should().BeTrue();
        payload.TargetSnapshotId.Should().NotBeNull();
        // The 200 acceptance body never leaks the score.
        (await response.Content.ReadAsStringAsync()).ToLowerInvariant().Should().NotContain("score");

        // Fact 1: the umbrella EvidenceSubmissionRegistered rides the QR path (Pending, per ADR-0010).
        var evidence = await DrainSingleMassTransitMessageAsync<EvidenceSubmissionRegisteredIntegrationEvent>(
            channel, evidenceQueue);
        evidence.LiveSessionId.Should().Be(seeded.LiveSessionId);
        evidence.TeamId.Should().Be(seeded.TeamId);
        evidence.SubmissionType.Should().Be(EvidenceSubmissionType.TreasureHuntQrScan);
        evidence.ValidationState.Should().Be(EvidenceValidationState.Pending);

        // Fact 2: TargetResolved is the second ordered fact — same EvidenceSubmissionId, carrying the
        // relayed score that the participant response withheld.
        var resolved = await DrainSingleMassTransitMessageAsync<TargetResolvedIntegrationEvent>(
            channel, targetResolvedQueue);
        resolved.LiveSessionId.Should().Be(seeded.LiveSessionId);
        resolved.TeamId.Should().Be(seeded.TeamId);
        resolved.ActiveSubstageId.Should().Be(seeded.TreasureHuntSubstageSnapshotId);
        resolved.EvidenceSubmissionId.Should().Be(evidence.EvidenceSubmissionId,
            "TargetResolved is the second fact of the SAME scan — both key off one EvidenceSubmissionId");
        resolved.ScoreValue.Should().Be(TargetScore,
            "TargetResolved relays the snapshotted target score verbatim to downstream scoring");
    }

    [Fact]
    public async Task RejectedScan_PublishesEvidenceSubmissionRegisteredOnly_EndToEnd()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        AddTrustedHeaders(seeded.ParticipantExternalIdentityId.ToString());

        await using var connection = await CreateBrokerConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var evidenceQueue = await DeclareAndBindAsync(channel, "session-evidence-submission-registered");
        var targetResolvedQueue = await DeclareAndBindAsync(channel, "session-target-resolved");

        var response = await _client.PostAsJsonAsync(BuildScansUrl(seeded), Scan(seeded, "QR-BOGUS"));

        // A wrong QR is retained-rejected and surfaces as RFC 7807 (422) — but the intake still commits.
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var evidence = await DrainSingleMassTransitMessageAsync<EvidenceSubmissionRegisteredIntegrationEvent>(
            channel, evidenceQueue);
        evidence.LiveSessionId.Should().Be(seeded.LiveSessionId);
        evidence.SubmissionType.Should().Be(EvidenceSubmissionType.TreasureHuntQrScan);

        // No TargetResolved fact for a rejected scan: give the delivery service ample time, then assert empty.
        await AssertNoMessageAsync(channel, targetResolvedQueue);
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

    private static async Task<string> DeclareAndBindAsync(IChannel channel, string exchangeName)
    {
        await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Fanout, durable: true, autoDelete: false);
        var queueName = $"e2e.{exchangeName}.{Guid.NewGuid():N}";
        await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(queueName, exchangeName, routingKey: string.Empty);
        return queueName;
    }

    private static async Task<T> DrainSingleMassTransitMessageAsync<T>(IChannel channel, string queueName)
    {
        var messages = new List<T>();
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var delivery = await channel.BasicGetAsync(queueName, autoAck: true);
            if (delivery is not null)
            {
                using var envelope = JsonDocument.Parse(delivery.Body);
                messages.Add(envelope.RootElement.GetProperty("message").Deserialize<T>(MassTransitJsonOptions)!);
            }
            else if (messages.Count > 0)
            {
                break;
            }
            else
            {
                await Task.Delay(200);
            }
        }

        messages.Should().ContainSingle(
            "the scan must publish exactly one {0} through MassTransit", typeof(T).Name);
        return messages[0];
    }

    private static async Task AssertNoMessageAsync(IChannel channel, string queueName)
    {
        // Poll long enough for the bus-outbox delivery service to have drained any pending row.
        for (var attempt = 0; attempt < 25; attempt++)
        {
            var delivery = await channel.BasicGetAsync(queueName, autoAck: true);
            delivery.Should().BeNull("a rejected scan must NOT publish a TargetResolved fact");
            await Task.Delay(200);
        }
    }

    private void AddTrustedHeaders(string userId)
    {
        _client.DefaultRequestHeaders.Remove("X-User-Id");
        _client.DefaultRequestHeaders.Remove("X-User-Role");
        _client.DefaultRequestHeaders.Remove("X-User-Email");
        _client.DefaultRequestHeaders.Add("X-User-Id", userId);
        _client.DefaultRequestHeaders.Add("X-User-Role", "Participant");
        _client.DefaultRequestHeaders.Add("X-User-Email", "participant@example.com");
    }

    private static RegisterTargetScanRequest Scan(SeededSession seeded, string scannedValue) =>
        new(seeded.ReferenceTeamId, scannedValue, null);

    private static string BuildScansUrl(SeededSession seeded) =>
        $"/api/sessions/{seeded.LiveSessionId:D}/participants/target-scans";

    private async Task<SeededSession> SeedActiveTreasureHuntSessionAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTimeOffset.UtcNow;
        var createdAt = now.AddMinutes(-20);
        var sourceMissionId = Guid.NewGuid();
        var activeSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);

        var snapshot = MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Treasure Mission",
            MaximumTime.Create(45),
            [
                StageSnapshot.Create("Stage One", 1, [activeSubstage])
            ],
            [
                TargetSnapshot.Create(activeSubstage.SubstageSnapshotId, "Target Alpha", CorrectQr, 1, true, TargetScore,
                    4.711, -74.0721, "Look under the stairs", "AfterPreviousTarget")
            ],
            []);

        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"THQ-{Guid.NewGuid():N}"[..12],
            "Target Scan E2E Session",
            maximumTimeMinutes: 45,
            createdAt,
            snapshot);
        var referenceTeamId = Guid.NewGuid();
        var team = session.AssociateTeam(referenceTeamId, "Red", "RED-01", 4);

        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(1), transitionPolicy);

        var participantExternalIdentityId = Guid.NewGuid();
        session.AdmitParticipant(
            participantExternalIdentityId,
            "Red One",
            team.TeamId,
            createdAt.AddMinutes(1).AddSeconds(30),
            new JoinPolicy());

        session.MoveTo(SessionState.Active, createdAt.AddMinutes(2), transitionPolicy);

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(
            session.LiveSessionId, team.TeamId, referenceTeamId, activeSubstage.SubstageSnapshotId, participantExternalIdentityId);
    }

    private sealed record SeededSession(
        Guid LiveSessionId,
        Guid TeamId,
        Guid ReferenceTeamId,
        Guid TreasureHuntSubstageSnapshotId,
        Guid ParticipantExternalIdentityId);

    private sealed record RegisterTargetScanRequest(
        Guid TeamId,
        string ScannedValue,
        string? Token);

    private sealed record RegisterTargetScanResponse(
        Guid LiveSessionId,
        Guid TeamId,
        Guid ActiveSubstageId,
        Guid? TargetSnapshotId,
        bool IsResolved,
        string? RejectionReason,
        DateTimeOffset SubmittedAt);
}
