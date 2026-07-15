using System.Diagnostics;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.IntegrationTests.Persistence;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Interceptors;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Messaging;

// Regression harness for the (fixed) 5s gameplay stall when RabbitMQ is unreachable.
//
// The fix moves integration-event publishing onto MassTransit's EF Core transactional bus outbox:
// DispatchDomainEventsInterceptor dispatches the integration publishers in SavingChanges (pre-commit),
// where IPublishEndpoint.Publish is a local OutboxMessage insert that rides the same transaction as
// the business write. The broker is reached later by the (unstarted here) delivery service, so a down
// broker is entirely off the write path — the write is prompt AND the event is durably retained.
//
// The seam under test is real: a DI-resolved ApplicationDbContext + the real interceptor + the real
// OutboxDomainEventDispatcher + the real publish handlers + the bus-outbox IPublishEndpoint. Nothing on
// the write path is mocked. A started in-memory bus backs the endpoint (MassTransit's Publish blocks on
// a bus-ready gate until the bus starts — an unstarted bus is NOT the same as an unavailable broker), but
// the hosted BusOutboxDeliveryService is never run, so enqueued OutboxMessage rows stay pending. That
// models a broker that never drains while proving the write itself never waits on the transport.
[Collection(PostgreSqlCollection.Name)]
public sealed class BrokerUnavailableGameplayStallTests
{
    // A committed gameplay write must return well under the old 5s publish stall. With the outbox the
    // write never awaits the broker; this budget separates the two regimes with margin for a real
    // Postgres round-trip.
    private static readonly TimeSpan PromptBudget = TimeSpan.FromSeconds(2);

    private static readonly DateTimeOffset ActiveAt = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly string _connectionString;
    private readonly PersistenceTestContextFactory _contextFactory;

    public BrokerUnavailableGameplayStallTests(PostgreSqlFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    // AC (a) mobile answer submission: an accepted answer's save returns promptly with the broker down.
    // Previously ~5s (the AnswerRegisteredEvent awaited a 5s-bounded publish inside SaveChanges); now the
    // publish is a local outbox insert, so the save never waits on the broker.
    [Fact]
    public async Task AcceptedAnswerSave_WhenBrokerUnavailable_ReturnsPromptlyInsteadOfStallingFiveSeconds()
    {
        var seeded = await SeedActiveTriviaQuestionSessionAsync();

        await using var host = await StartOutboxHostAsync();
        using var scope = host.CreateScope();
        var repository = new LiveSessionRepository(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        session!.RegisterTriviaAnswer(seeded.TeamId, 1, Guid.NewGuid(), ActiveAt.AddSeconds(5));

        var elapsed = await TimeAsync(() => repository.UpdateAsync(session, CancellationToken.None));

        await AssertAnswerCommittedAsync(seeded.LiveSessionId);
        elapsed.Should().BeLessThan(
            PromptBudget,
            "a down broker must not stall the accepted-answer save — the outbox publish is a local insert");
    }

    // AC (b) question close/advance: an expired question closes and question 2 activates promptly with
    // the broker down. Previously ~5s (the persist that raised QuestionClosedEvent blocked on the 5s
    // publish before the broadcast/activation); now that persist enqueues the event into the outbox.
    [Fact]
    public async Task ExpiredQuestionCloseAndAdvance_WhenBrokerUnavailable_ActivatesNextQuestionPromptly()
    {
        var seeded = await SeedActiveTriviaQuestionSessionAsync();
        var broadcaster = new Mock<ISessionQuestionBroadcaster>();

        await using var host = await StartOutboxHostAsync();
        using var scope = host.CreateScope();
        var repository = new LiveSessionRepository(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        var facade = new TriviaRoundOrchestratorFacade(
            repository,
            broadcaster.Object,
            new SequentialQuestionActivationStrategy(),
            new SessionStateTransitionPolicy());

        // now is past the 30s question-0 window, so the close is an expiry-driven close.
        var elapsed = await TimeAsync(
            () => facade.CloseAndAdvanceAsync(session!, ActiveAt.AddSeconds(31), CancellationToken.None));

        session!.ActiveQuestionIndex.Should().Be(1);
        broadcaster.Verify(
            current => current.BroadcastQuestionActivatedAsync(
                It.Is<QuestionActivatedNotificationDto>(notification => notification.QuestionIndex == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
        elapsed.Should().BeLessThan(
            PromptBudget,
            "a down broker must not delay closing question 1 and activating question 2 — the publish is an outbox insert");
    }

    // Control: the accepted-answer save returns promptly AND durably captures the integration event.
    // "Publishes" now means "an outbox row is written" (drained to the broker later by the delivery
    // service), not "a broker mock was invoked".
    [Fact]
    public async Task AcceptedAnswerSave_WhenBrokerAvailable_ReturnsPromptlyAndPublishes()
    {
        var seeded = await SeedActiveTriviaQuestionSessionAsync();

        await using var host = await StartOutboxHostAsync();
        using var scope = host.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repository = new LiveSessionRepository(context);
        var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        session!.RegisterTriviaAnswer(seeded.TeamId, 1, Guid.NewGuid(), ActiveAt.AddSeconds(5));

        var elapsed = await TimeAsync(() => repository.UpdateAsync(session, CancellationToken.None));

        await AssertAnswerCommittedAsync(seeded.LiveSessionId);
        (await CountPendingOutboxMessagesAsync(context, nameof(AnswerRegisteredIntegrationEvent)))
            .Should().Be(1, "the accepted-answer publish is captured as one outbox row");
        elapsed.Should().BeLessThan(PromptBudget);
    }

    // Control: the close/advance activates question 2 promptly AND writes the QuestionClosed outbox row.
    [Fact]
    public async Task ExpiredQuestionCloseAndAdvance_WhenBrokerAvailable_ActivatesNextQuestionPromptlyAndPublishes()
    {
        var seeded = await SeedActiveTriviaQuestionSessionAsync();
        var broadcaster = new Mock<ISessionQuestionBroadcaster>();

        await using var host = await StartOutboxHostAsync();
        using var scope = host.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repository = new LiveSessionRepository(context);
        var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        var facade = new TriviaRoundOrchestratorFacade(
            repository,
            broadcaster.Object,
            new SequentialQuestionActivationStrategy(),
            new SessionStateTransitionPolicy());

        var elapsed = await TimeAsync(
            () => facade.CloseAndAdvanceAsync(session!, ActiveAt.AddSeconds(31), CancellationToken.None));

        session!.ActiveQuestionIndex.Should().Be(1);
        (await CountPendingOutboxMessagesAsync(context, nameof(QuestionClosedIntegrationEvent)))
            .Should().Be(1, "closing the question is captured as one outbox row");
        elapsed.Should().BeLessThan(PromptBudget);
    }

    // AC (c) "durably pending": with the broker down the answer commits AND the integration event is
    // retained as a pending outbox row for the AnswerRegisteredIntegrationEvent — no silent loss.
    // (Formerly asserted no outbox existed; the transactional outbox now closes that window.)
    // Delivery-on-recovery is intentionally NOT asserted here: the hosted BusOutboxDeliveryService is
    // not run by this direct-context harness (§7). The outbox → delivery-service → broker → consumer drain
    // is covered by OutboxDeliveryOnRecoveryTests. (MassTransitQuestionClosedPublishTests /
    // MassTransitRemainingIntegrationEventPublishTests publish through a raw endpoint and bypass the outbox,
    // so they do NOT exercise the delivery service.)
    [Fact]
    public async Task AcceptedAnswerSave_WhenBrokerUnavailable_RetainsPendingOutboxMessageForDelivery()
    {
        var seeded = await SeedActiveTriviaQuestionSessionAsync();

        await using var host = await StartOutboxHostAsync();
        using var scope = host.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repository = new LiveSessionRepository(context);
        var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        session!.RegisterTriviaAnswer(seeded.TeamId, 1, Guid.NewGuid(), ActiveAt.AddSeconds(5));

        await repository.UpdateAsync(session, CancellationToken.None);

        await AssertAnswerCommittedAsync(seeded.LiveSessionId);

        // Read the outbox from a fresh connection to prove the row is durably committed, not just tracked.
        await using var verifyContext = _contextFactory.Create();
        var pending = await verifyContext.Set<OutboxMessage>()
            .Where(message => message.MessageType.Contains(nameof(AnswerRegisteredIntegrationEvent)))
            .ToListAsync();
        pending.Should().ContainSingle(
            "with the broker down the AnswerRegistered fact must survive as a pending outbox row until delivery");
    }

    // Builds the real production write-path seam over the shared Postgres: a DI-managed
    // ApplicationDbContext with the audit + domain-event interceptors, the MassTransit EF Core bus
    // outbox (never started — the insert never touches RabbitMQ), and the real transactional publishers.
    // The post-commit MediatR notification phase is a no-op here (no SignalR broadcasters in this seam).
    private ServiceProvider BuildOutboxProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICurrentUser>(_ => TestCurrentUser.Default);

        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<DispatchDomainEventsInterceptor>();

        // Add only the app's own interceptors by concrete type — mirrors production. Resolving the whole
        // ISaveChangesInterceptor collection would drag in MassTransit's bus-outbox interceptor, which
        // resolves ApplicationDbContext and re-enters this factory (StackOverflow). MassTransit attaches
        // its own outbox interceptor to the context via AddEntityFrameworkOutbox below.
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
        services.AddScoped<PublishEvidenceSubmissionAcceptedIntegrationEventHandler>();
        services.AddScoped<PublishEvidenceSubmissionRejectedIntegrationEventHandler>();
        services.AddScoped<PublishQuestionClosedIntegrationEventHandler>();
        services.AddScoped<PublishSessionResultsFinalizedIntegrationEventHandler>();
        services.AddScoped<PublishSessionStateChangedIntegrationEventHandler>();
        services.AddScoped<PublishTargetResolvedIntegrationEventHandler>();
        services.AddScoped<PublishLiveSessionOperatorAssignedIntegrationEventHandler>();
        services.AddScoped<IOutboxDomainEventDispatcher, OutboxDomainEventDispatcher>();
        services.AddScoped<IMediator, NoOpMediator>();

        services.AddMassTransit(bus =>
        {
            bus.AddEntityFrameworkOutbox<ApplicationDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });

            bus.UsingInMemory((_, _) => { });
        });

        return services.BuildServiceProvider();
    }

    // Builds the outbox provider and starts its in-memory bus so the write path's Publish clears the
    // MassTransit bus-ready gate. The bus-outbox delivery service is a hosted service and is not run by
    // this direct-context harness, so OutboxMessage rows enqueued during the save stay pending.
    private async Task<StartedOutboxHost> StartOutboxHostAsync()
    {
        var provider = BuildOutboxProvider();
        var bus = provider.GetRequiredService<IBusControl>();
        await bus.StartAsync();
        return new StartedOutboxHost(provider, bus);
    }

    // Owns the started bus + provider; stops the bus before disposing the provider so no in-memory
    // receive loop outlives the test.
    private sealed class StartedOutboxHost : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IBusControl _bus;

        public StartedOutboxHost(ServiceProvider provider, IBusControl bus)
        {
            _provider = provider;
            _bus = bus;
        }

        public IServiceScope CreateScope() => _provider.CreateScope();

        public async ValueTask DisposeAsync()
        {
            await _bus.StopAsync();
            await _provider.DisposeAsync();
        }
    }

    private static async Task<int> CountPendingOutboxMessagesAsync(ApplicationDbContext context, string messageTypeFragment)
    {
        return await context.Set<OutboxMessage>()
            .Where(message => message.MessageType.Contains(messageTypeFragment))
            .CountAsync();
    }

    private static async Task<TimeSpan> TimeAsync(Func<Task> action)
    {
        var stopwatch = Stopwatch.StartNew();
        await action();
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private async Task AssertAnswerCommittedAsync(Guid liveSessionId)
    {
        await using var assertContext = _contextFactory.Create();
        var reloaded = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSessionId, CancellationToken.None);
        reloaded!.TriviaAnswerSubmissions.Should().ContainSingle(
            "the answer must commit atomically with its outbox row even when the broker is unreachable");
    }

    // A single-substage, two-question trivia session in Active with question 0 activated at ActiveAt:
    // closing question 0 activates question 1, and the active window admits an answer at ActiveAt+5s.
    private async Task<SeededSession> SeedActiveTriviaQuestionSessionAsync()
    {
        await using var resetContext = _contextFactory.Create();
        await resetContext.LiveSessions.ExecuteDeleteAsync();
        // Clear the shared outbox tables too: rows persist across tests in the collection, so the
        // per-message-type count/single assertions would otherwise see leftovers from earlier tests.
        // OutboxMessage first (it references OutboxState).
        await resetContext.Set<OutboxMessage>().ExecuteDeleteAsync();
        await resetContext.Set<OutboxState>().ExecuteDeleteAsync();

        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Broker Stall Trivia Session",
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
