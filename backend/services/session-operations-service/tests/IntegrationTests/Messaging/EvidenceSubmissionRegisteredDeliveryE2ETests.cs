using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.IntegrationTests.Persistence;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Interceptors;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Messaging;

// HU-29 X.4 — proves EvidenceSubmissionRegisteredIntegrationEvent travels from the transactional bus
// outbox through the delivery service to RabbitMQ to a bound consumer. The trivia answer path raises
// TWO facts: AnswerRegistered (scoring, already proven by OutboxDeliveryOnRecoveryTests) and the
// umbrella EvidenceSubmissionRegistered (audit/history/notification). This test proves the umbrella
// fact survives the full outbox → delivery-service → broker → consumer loop end-to-end.
//
// Two phases over the SHARED Postgres, both wired to a REAL RabbitMQ container:
//   Phase 1 (accumulate): the write-path seam (interceptor → OutboxDomainEventDispatcher →
//     publish handlers → bus-outbox IPublishEndpoint) captures the trivia answer. The bus is started
//     so Publish clears the bus-ready gate, but NO delivery service runs, so outbox rows stay pending.
//   Phase 2 (recovery): a generic host with the same RabbitMQ config, a bound
//     EvidenceSubmissionRegistered consumer, AND a running BusOutboxDeliveryService starts.
//     The delivery service claims the pending row from Postgres, publishes it to RabbitMQ, and the
//     consumer receives it — proving outbox → delivery-service → broker → consumer end to end.
//
// Skips gracefully when Docker is unavailable.
[Collection(PostgreSqlCollection.Name)]
public sealed class EvidenceSubmissionRegisteredDeliveryE2ETests
{
    private static readonly DateTimeOffset ActiveAt = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly string _connectionString;
    private readonly PersistenceTestContextFactory _contextFactory;

    public EvidenceSubmissionRegisteredDeliveryE2ETests(PostgreSqlFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task PendingOutboxRow_IsDrainedToBoundConsumer_WhenDeliveryServiceRecovers()
    {
        var rabbit = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();
        await DockerAvailability.StartOrSkipAsync(() => rabbit.StartAsync(), rabbit.DisposeAsync);

        try
        {
            var seeded = await SeedActiveTriviaQuestionSessionAsync();

            // Phase 1 — accumulate: write goes through the outbox, no delivery service drains it.
            await CaptureTriviaAnswerAsPendingOutboxRowsAsync(rabbit, seeded);

            // Both outbox rows are durably pending before any delivery service runs.
            await using (var verifyContext = _contextFactory.Create())
            {
                var pendingEvidence = await verifyContext.Set<OutboxMessage>()
                    .Where(message => message.MessageType.Contains(nameof(EvidenceSubmissionRegisteredIntegrationEvent)))
                    .ToListAsync();
                pendingEvidence.Should().ContainSingle(
                    "phase 1 must capture the EvidenceSubmissionRegistered fact as a pending outbox row");

                var pendingAnswer = await verifyContext.Set<OutboxMessage>()
                    .Where(message => message.MessageType.Contains(nameof(AnswerRegisteredIntegrationEvent)))
                    .ToListAsync();
                pendingAnswer.Should().ContainSingle(
                    "phase 1 must also capture the AnswerRegistered fact — both ride the same outbox");
            }

            // Phase 2 — recovery: a real host with a running BusOutboxDeliveryService + bound consumer.
            var probe = new EvidenceSubmissionRegisteredProbe();
            using var host = BuildRecoveryHost(rabbit, probe);
            await host.StartAsync();
            try
            {
                var received = await probe.Received.Task.WaitAsync(TimeSpan.FromSeconds(30));

                received.LiveSessionId.Should().Be(seeded.LiveSessionId);
                received.TeamId.Should().Be(seeded.TeamId);
                received.SubmissionType.Should().Be(EvidenceSubmissionType.TriviaAnswer);
                received.ValidationState.Should().Be(EvidenceValidationState.Pending,
                    "the umbrella intake fact always carries Pending even when the trivia path accepts");
                received.ActiveSubstageId.Should().NotBeEmpty();

                // The delivery service removes the OutboxMessage row once it is delivered; poll until drained.
                await AssertOutboxRowDrainedAsync();
            }
            finally
            {
                await host.StopAsync();
            }
        }
        finally
        {
            await rabbit.DisposeAsync();
        }
    }

    // Phase 1: build the real write-path seam over the shared Postgres with a bus-outbox IPublishEndpoint
    // backed by the real RabbitMQ container, start the bus so Publish clears the bus-ready gate, and save
    // a trivia answer. The bus-outbox delivery service is NOT registered/started here, so the enqueued
    // OutboxMessage rows stay pending.
    private async Task CaptureTriviaAnswerAsPendingOutboxRowsAsync(RabbitMqContainer rabbit, SeededSession seeded)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICurrentUser>(_ => TestCurrentUser.Default);

        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<DispatchDomainEventsInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<DispatchDomainEventsInterceptor>());
            options.UseNpgsql(_connectionString);
        });

        services.AddScoped<ILiveSessionRepository, LiveSessionRepository>();
        services.AddScoped<PublishAnswerRegisteredIntegrationEventHandler>();
        services.AddScoped<PublishEvidenceSubmissionRegisteredIntegrationEventHandler>();
        services.AddScoped<PublishQuestionClosedIntegrationEventHandler>();
        services.AddScoped<PublishSessionResultsFinalizedIntegrationEventHandler>();
        services.AddScoped<IOutboxDomainEventDispatcher, OutboxDomainEventDispatcher>();
        services.AddScoped<IMediator, NoOpMediator>();

        services.AddMassTransit(bus =>
        {
            bus.AddEntityFrameworkOutbox<ApplicationDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });

            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbit.Hostname, rabbit.GetMappedPublicPort(5672), "/", host =>
                {
                    host.Username("guest");
                    host.Password("guest");
                });
                cfg.ConfigureEndpoints(context);
            });
        });

        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IBusControl>();
        await bus.StartAsync();
        try
        {
            using var scope = provider.CreateScope();
            var repository = new LiveSessionRepository(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
            var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
            session!.RegisterTriviaAnswer(seeded.TeamId, 1, Guid.NewGuid(), ActiveAt.AddSeconds(5));
            await repository.UpdateAsync(session, CancellationToken.None);
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    // Phase 2: a generic host whose MassTransitHostedService starts the bus + consumer endpoint and whose
    // BusOutboxDeliveryService (via UseBusOutbox) polls the shared Postgres outbox and drains the pending
    // EvidenceSubmissionRegistered row to RabbitMQ, where the bound consumer receives it.
    private IHost BuildRecoveryHost(RabbitMqContainer rabbit, EvidenceSubmissionRegisteredProbe probe)
    {
        return new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(probe);

                services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(_connectionString));

                services.AddMassTransit(bus =>
                {
                    bus.AddEntityFrameworkOutbox<ApplicationDbContext>(outbox =>
                    {
                        outbox.UsePostgres();
                        outbox.UseBusOutbox();
                        outbox.QueryDelay = TimeSpan.FromSeconds(1);
                    });

                    bus.AddConsumer<EvidenceSubmissionRegisteredTestConsumer>();
                    bus.UsingRabbitMq((context, cfg) =>
                    {
                        cfg.Host(rabbit.Hostname, rabbit.GetMappedPublicPort(5672), "/", host =>
                        {
                            host.Username("guest");
                            host.Password("guest");
                        });
                        cfg.ConfigureEndpoints(context);
                    });
                });
            })
            .Build();
    }

    private async Task AssertOutboxRowDrainedAsync()
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await using var context = _contextFactory.Create();
            var remaining = await context.Set<OutboxMessage>()
                .CountAsync(message => message.MessageType.Contains(nameof(EvidenceSubmissionRegisteredIntegrationEvent)));
            if (remaining == 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        await using var finalContext = _contextFactory.Create();
        var stillPending = await finalContext.Set<OutboxMessage>()
            .CountAsync(message => message.MessageType.Contains(nameof(EvidenceSubmissionRegisteredIntegrationEvent)));
        stillPending.Should().Be(0, "the delivery service must remove the outbox row once it is delivered to the broker");
    }

    private sealed class EvidenceSubmissionRegisteredProbe
    {
        public TaskCompletionSource<EvidenceSubmissionRegisteredIntegrationEvent> Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class EvidenceSubmissionRegisteredTestConsumer : IConsumer<EvidenceSubmissionRegisteredIntegrationEvent>
    {
        private readonly EvidenceSubmissionRegisteredProbe _probe;

        public EvidenceSubmissionRegisteredTestConsumer(EvidenceSubmissionRegisteredProbe probe) => _probe = probe;

        public Task Consume(ConsumeContext<EvidenceSubmissionRegisteredIntegrationEvent> context)
        {
            _probe.Received.TrySetResult(context.Message);
            return Task.CompletedTask;
        }
    }

    // A single-substage, two-question trivia session in Active with question 0 activated at ActiveAt; the
    // active window admits an answer at ActiveAt+5s. Clears the shared LiveSessions + outbox tables first.
    private async Task<SeededSession> SeedActiveTriviaQuestionSessionAsync()
    {
        await using var resetContext = _contextFactory.Create();
        await resetContext.LiveSessions.ExecuteDeleteAsync();
        await resetContext.Set<OutboxMessage>().ExecuteDeleteAsync();
        await resetContext.Set<OutboxState>().ExecuteDeleteAsync();

        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Evidence Intake E2E Trivia Session",
            20,
            ActiveAt.AddMinutes(-10),
            CreateTriviaRuntimeSnapshot(sourceMissionId));

        var transitionPolicy = new SessionStateTransitionPolicy();
        session.AssociateTeam(Guid.NewGuid(), "Aurora", "AUR-01", 3);
        session.MoveTo(SessionState.Preparing, ActiveAt.AddMinutes(-1), transitionPolicy);
        session.MoveTo(SessionState.Active, ActiveAt, transitionPolicy);
        session.ActivateQuestion(0, ActiveAt);

        await using var seedContext = _contextFactory.Create();
        seedContext.LiveSessions.Add(session);
        await seedContext.SaveChangesAsync(CancellationToken.None);

        return new SeededSession(session.LiveSessionId, session.Teams.Single().TeamId);
    }

    private static MissionRuntimeSnapshot CreateTriviaRuntimeSnapshot(Guid sourceMissionId)
    {
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Mission Runtime",
            MaximumTime.Create(20),
            [
                StageSnapshot.Create("Stage One", 1, [triviaSubstage])
            ],
            [],
            [
                TriviaQuestionSnapshot.Create(
                    triviaSubstage.SubstageSnapshotId,
                    "Capital of France?",
                    1,
                    50,
                    30,
                    "Paris is the capital city.",
                    [
                        TriviaOptionSnapshot.Create("Paris", 1, true),
                        TriviaOptionSnapshot.Create("Lyon", 2, false)
                    ]),
                TriviaQuestionSnapshot.Create(
                    triviaSubstage.SubstageSnapshotId,
                    "Capital of Spain?",
                    2,
                    50,
                    25,
                    "Madrid is the capital city.",
                    [
                        TriviaOptionSnapshot.Create("Madrid", 1, true),
                        TriviaOptionSnapshot.Create("Barcelona", 2, false)
                    ])
            ]);
    }

    private sealed record SeededSession(Guid LiveSessionId, Guid TeamId);
}
