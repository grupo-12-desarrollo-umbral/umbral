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

// HU-29 X.3 — proves the generic evidence-intake fact rides the SAME transactional bus outbox HU-34
// wired for AnswerRegistered. Registering a trivia answer now raises TWO facts (ADR-0010): the
// specialized AnswerRegistered (scoring) AND the umbrella EvidenceSubmissionRegistered (audit/history/
// notification/projection). This exercises the real production write-path seam (DI-managed
// ApplicationDbContext + real interceptor + real OutboxDomainEventDispatcher + real publish handlers +
// bus-outbox IPublishEndpoint) and asserts the umbrella fact is captured as a pre-commit OutboxMessage
// atomically with the business write — no second publisher stack, no exchange bootstrap.
//
// Mirrors BrokerUnavailableGameplayStallTests' outbox harness (the AnswerRegistered publish/outbox test).
[Collection(PostgreSqlCollection.Name)]
public sealed class EvidenceSubmissionRegisteredOutboxTests
{
    private static readonly DateTimeOffset ActiveAt = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly string _connectionString;
    private readonly PersistenceTestContextFactory _contextFactory;

    public EvidenceSubmissionRegisteredOutboxTests(PostgreSqlFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    // The umbrella EvidenceSubmissionRegistered fact is enqueued as exactly one outbox row, committed
    // atomically with the trivia-answer business write, and carries ValidationState = Pending even though
    // the trivia path resolves the specialization to Accepted (the base intake fact is always Pending).
    [Fact]
    public async Task RegisterTriviaAnswer_InsertsEvidenceSubmissionRegisteredOutboxRow_WithPendingState()
    {
        var seeded = await SeedActiveTriviaQuestionSessionAsync();

        await using var host = await StartOutboxHostAsync();
        using var scope = host.CreateScope();
        var repository = new LiveSessionRepository(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        session!.RegisterTriviaAnswer(seeded.TeamId, 1, Guid.NewGuid(), ActiveAt.AddSeconds(5));

        await repository.UpdateAsync(session, CancellationToken.None);

        // The business write committed atomically with its outbox row.
        await using var assertContext = _contextFactory.Create();
        var reloaded = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        reloaded!.TriviaAnswerSubmissions.Should().ContainSingle(
            "the trivia answer must commit atomically with the umbrella evidence-intake outbox row");

        // Read the outbox from a fresh connection to prove the row is durably committed, not just tracked.
        await using var verifyContext = _contextFactory.Create();
        var evidenceRows = await verifyContext.Set<OutboxMessage>()
            .Where(message => message.MessageType.Contains(nameof(EvidenceSubmissionRegisteredIntegrationEvent)))
            .ToListAsync();

        evidenceRows.Should().ContainSingle(
            "the successful base-evidence registration must enqueue exactly one EvidenceSubmissionRegistered outbox row");
        evidenceRows.Single().Body.Should().Contain(
            "\"validationState\": 0",
            "the umbrella intake fact stays Pending even on the accepted trivia path (ADR-0010 two-facts model)");

        // The specialized AnswerRegistered fact still rides the same outbox — the umbrella event is additive.
        (await verifyContext.Set<OutboxMessage>()
                .Where(message => message.MessageType.Contains(nameof(AnswerRegisteredIntegrationEvent)))
                .CountAsync())
            .Should().Be(1, "the specialized AnswerRegistered fact is retained alongside the umbrella fact");
    }

    // Builds the real production write-path seam over the shared Postgres: a DI-managed
    // ApplicationDbContext with the audit + domain-event interceptors, the MassTransit EF Core bus outbox
    // (never drained here — the insert never touches RabbitMQ), and the real transactional publishers.
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
        // resolves ApplicationDbContext and re-enters this factory (StackOverflow).
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
    // MassTransit bus-ready gate. The bus-outbox delivery service is not run here, so OutboxMessage rows
    // enqueued during the save stay pending — exactly what this test reads back.
    private async Task<StartedOutboxHost> StartOutboxHostAsync()
    {
        var provider = BuildOutboxProvider();
        var bus = provider.GetRequiredService<IBusControl>();
        await bus.StartAsync();
        return new StartedOutboxHost(provider, bus);
    }

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

    // A single-substage, two-question trivia session in Active with question 0 activated at ActiveAt; the
    // active window admits an answer at ActiveAt+5s.
    private async Task<SeededSession> SeedActiveTriviaQuestionSessionAsync()
    {
        await using var resetContext = _contextFactory.Create();
        await resetContext.LiveSessions.ExecuteDeleteAsync();
        // Clear the shared outbox tables: rows persist across tests in the collection, so the
        // per-message-type single/count assertions would otherwise see leftovers.
        // OutboxMessage first (it references OutboxState).
        await resetContext.Set<OutboxMessage>().ExecuteDeleteAsync();
        await resetContext.Set<OutboxState>().ExecuteDeleteAsync();

        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Evidence Intake Trivia Session",
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
