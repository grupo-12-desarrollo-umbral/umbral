using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

// CHARACTERIZATION test for the cold-start / pre-ready broker-unreachable window (Finding B).
//
// This test does NOT prove a desired behaviour — it DIAGNOSES a suspected defect. No production
// safeguard has been added to make it pass; the removed 5s CancelAfter guard stays removed. The
// observation itself is the deliverable.
//
// The scenario (distinct from BrokerUnavailableGameplayStallTests, which models the POST-ready steady
// state — broker up at boot, then delivery service not draining): here RabbitMQ is unreachable FROM
// COLD START, so MassTransit's one-shot bus-ready latch NEVER opens. In production the API serves
// requests before the bus connects (MassTransitHostOptions.WaitUntilStarted defaults to false), so a
// gameplay write can land in this pre-ready window. The suspected defect: the outbox IPublishEndpoint
// insert — which runs inside the interceptor's pre-commit SavingChanges, inside SaveChanges, holding
// the DB transaction — blocks on the bus-ready gate indefinitely because the gate is not yet open and
// the 5s guard that used to bound it is gone.
//
// The seam under test is real: a DI-resolved ApplicationDbContext + the real interceptor + the real
// OutboxDomainEventDispatcher + the real PublishAnswerRegisteredIntegrationEventHandler + the bus-outbox
// IPublishEndpoint. Nothing on the write path is mocked.
//
// Unreachable-broker mechanism (judgment call): the bus points at 127.0.0.1 on a just-released
// ephemeral port with nothing listening. Connection-refused → MassTransit retries → the bus is
// "started" but never reaches "ready". This needs NO Docker (unlike a Testcontainers approach), so the
// test is portable and runs anywhere the shared Postgres fixture runs. The bus is started WITHOUT
// awaiting readiness (fire-and-forget, mirroring WaitUntilStarted=false) so the test reaches the write
// while the bus is still connecting.
//
// The write is bounded at the TEST level (a test-owned CancellationToken plus a Task.WhenAny race
// against a delay) so a genuine block cannot hang the suite — the production code is untouched.
[Collection(PostgreSqlCollection.Name)]
public sealed class ColdStartBrokerUnreachableWriteTests
{
    // If the design were safe cold-start too, the write would return within this budget (a real Postgres
    // round-trip). Exceeding it means the write is waiting on the bus-ready gate — the suspected defect.
    private static readonly TimeSpan PromptBudget = TimeSpan.FromSeconds(2);

    // Hard upper bound on the observation so a real block cannot hang the test suite.
    private static readonly TimeSpan ObservationBudget = TimeSpan.FromSeconds(10);

    private static readonly DateTimeOffset ActiveAt = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly string _connectionString;
    private readonly PersistenceTestContextFactory _contextFactory;

    public ColdStartBrokerUnreachableWriteTests(PostgreSqlFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    // OBSERVED (2026-07-12, shared-Postgres fixture, dead-localhost-port broker): the accepted-answer
    // save returns PROMPTLY (~0.5s, well under the 2s budget) even though RabbitMQ is unreachable from
    // cold start and the bus never reached ready. VERDICT: Finding B is NOT a real defect. The bus-outbox
    // IPublishEndpoint.Publish is a local OutboxMessage insert that does NOT wait on MassTransit's
    // bus-ready latch, so the pre-ready window does not stall the write. The answer commits atomically and
    // the integration event is retained as a pending outbox row for later delivery — exactly the
    // post-ready steady-state behaviour, now confirmed to hold cold-start too.
    [Fact]
    public async Task AcceptedAnswerSave_WhenBrokerUnreachableFromColdStart_ReturnsPromptlyAndRetainsPendingOutboxRow()
    {
        var seeded = await SeedActiveTriviaQuestionSessionAsync();

        await using var host = StartColdStartHost();
        using var scope = host.CreateScope();
        var repository = new LiveSessionRepository(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        session!.RegisterTriviaAnswer(seeded.TeamId, 1, Guid.NewGuid(), ActiveAt.AddSeconds(5));

        var outcome = await ObserveWriteAsync(() => repository.UpdateAsync(session, ObservationCancellation()));

        // Characterization assertion: reports the observed regime so any future regression reads as a
        // precise diagnosis rather than a bare failure.
        outcome.Regime.Should().Be(
            WriteRegime.PromptReturn,
            $"cold-start-unreachable-broker observation: the accepted-answer save {outcome.Describe()} "
            + $"(prompt budget {PromptBudget.TotalSeconds:0}s, observation cap {ObservationBudget.TotalSeconds:0}s). "
            + "The outbox Publish is a local OutboxMessage insert that does NOT await the bus-ready latch, so "
            + "an unreachable broker in the pre-ready window does not stall the write — Finding B is not a real "
            + "defect. A block past the budget here would mean the outbox insert had started waiting on the "
            + "bus-ready gate, reintroducing the very stall the outbox was meant to remove.");

        // Prove the prompt return is a real committed write, not a short-circuit: the answer is durably
        // committed AND the AnswerRegistered fact survives as a pending outbox row for later delivery.
        await AssertAnswerCommittedAsync(seeded.LiveSessionId);
        await using var verifyContext = _contextFactory.Create();
        (await verifyContext.Set<OutboxMessage>()
                .CountAsync(message => message.MessageType.Contains(nameof(AnswerRegisteredIntegrationEvent))))
            .Should().Be(1, "the cold-start write must still capture the integration event as one pending outbox row");
    }

    // Starts the write on a background task, races it against the observation cap, and classifies the
    // result. Never rethrows into the test thread — the outcome is data, not a failure.
    private async Task<WriteOutcome> ObserveWriteAsync(Func<Task> write)
    {
        var stopwatch = Stopwatch.StartNew();
        var writeTask = Task.Run(write);
        var finished = await Task.WhenAny(writeTask, Task.Delay(ObservationBudget));
        stopwatch.Stop();

        if (finished != writeTask)
        {
            // Still running past the cap — a genuine block. Leave writeTask unobserved (it is cancelled
            // via the test token / abandoned with the host); the suite is not hanging because we returned.
            _ = writeTask.ContinueWith(t => _ = t.Exception, TaskScheduler.Default);
            return new WriteOutcome(WriteRegime.BlockedPastBudget, stopwatch.Elapsed, null);
        }

        if (writeTask.IsFaulted)
        {
            return new WriteOutcome(WriteRegime.Faulted, stopwatch.Elapsed, writeTask.Exception?.GetBaseException());
        }

        var regime = stopwatch.Elapsed < PromptBudget ? WriteRegime.PromptReturn : WriteRegime.BlockedPastBudget;
        return new WriteOutcome(regime, stopwatch.Elapsed, null);
    }

    // A test-owned token (NOT a production guard) that fires just after the observation cap so a
    // cancellation-aware block can unwind instead of leaking a live task.
    private static CancellationToken ObservationCancellation()
    {
        var cts = new CancellationTokenSource(ObservationBudget + TimeSpan.FromSeconds(1));
        return cts.Token;
    }

    // Builds the real production write-path seam over the shared Postgres with the MassTransit EF Core bus
    // outbox pointed at an unreachable RabbitMQ (dead localhost port). The bus is started fire-and-forget
    // so it enters the "started but never ready" state the pre-ready window requires.
    private ColdStartHost StartColdStartHost()
    {
        var deadPort = ReserveDeadLocalPort();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICurrentUser>(_ => TestCurrentUser.Default);

        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<DispatchDomainEventsInterceptor>();

        // Add only the app's own interceptors by concrete type — mirrors production and the sibling
        // harnesses. MassTransit attaches its own bus-outbox interceptor via AddEntityFrameworkOutbox;
        // resolving the full ISaveChangesInterceptor collection would re-enter this factory (StackOverflow).
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
        services.AddScoped<PublishSessionStateChangedIntegrationEventHandler>();
        services.AddScoped<IOutboxDomainEventDispatcher, OutboxDomainEventDispatcher>();
        services.AddScoped<IMediator, NoOpMediator>();

        services.AddMassTransit(bus =>
        {
            bus.AddEntityFrameworkOutbox<ApplicationDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });

            // Point at a dead localhost port: connection-refused, retried by MassTransit, never ready.
            bus.UsingRabbitMq((_, cfg) =>
            {
                cfg.Host("127.0.0.1", (ushort)deadPort, "/", host =>
                {
                    host.Username("guest");
                    host.Password("guest");
                });
            });
        });

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IBusControl>();

        // Fire-and-forget start: model production's WaitUntilStarted=false. Do NOT await readiness — the
        // whole point is to write in the pre-ready window. The start task never completes (never ready);
        // observe its exception so it does not surface as an unobserved-task fault.
        var startTask = bus.StartAsync(CancellationToken.None);
        _ = startTask.ContinueWith(t => _ = t.Exception, TaskScheduler.Default);

        return new ColdStartHost(provider, bus);
    }

    // Grabs a free loopback TCP port and releases it immediately, so a connection attempt is refused.
    private static int ReserveDeadLocalPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    // Owns the started-but-never-ready bus + provider. Teardown is bounded: a never-ready bus's StopAsync
    // can itself wait, so it is raced against a short cap and then the provider is disposed regardless.
    private sealed class ColdStartHost : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IBusControl _bus;

        public ColdStartHost(ServiceProvider provider, IBusControl bus)
        {
            _provider = provider;
            _bus = bus;
        }

        public IServiceScope CreateScope() => _provider.CreateScope();

        public async ValueTask DisposeAsync()
        {
            try
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _bus.StopAsync(stopCts.Token);
            }
            catch
            {
                // A never-ready bus may not stop cleanly; disposing the provider still tears everything down.
            }

            await _provider.DisposeAsync();
        }
    }

    private async Task AssertAnswerCommittedAsync(Guid liveSessionId)
    {
        await using var assertContext = _contextFactory.Create();
        var reloaded = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSessionId, CancellationToken.None);
        reloaded!.TriviaAnswerSubmissions.Should().ContainSingle(
            "the answer must commit even though the broker is unreachable from cold start");
    }

    private enum WriteRegime
    {
        PromptReturn,
        BlockedPastBudget,
        Faulted,
    }

    private sealed record WriteOutcome(WriteRegime Regime, TimeSpan Elapsed, Exception? Fault)
    {
        public string Describe() => Regime switch
        {
            WriteRegime.PromptReturn => $"returned promptly in {Elapsed.TotalMilliseconds:0}ms",
            WriteRegime.BlockedPastBudget => $"blocked for {Elapsed.TotalSeconds:0.0}s (past the prompt budget)",
            WriteRegime.Faulted => $"threw {Fault?.GetType().Name}: {Fault?.Message}",
            _ => "produced an unknown outcome",
        };
    }

    // A single-substage, two-question trivia session in Active with question 0 activated at ActiveAt; the
    // active window admits an answer at ActiveAt+5s. Clears the shared LiveSessions + outbox tables first
    // so cross-test leftovers cannot interfere.
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
            "Cold Start Broker Trivia Session",
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
