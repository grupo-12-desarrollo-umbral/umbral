using System.Reflection;
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

// HU-30 X.3 — proves the rejection_reason column round-trips through EF persistence and that the
// transactional outbox inserts EvidenceSubmissionRegistered atomically with the business write.
// Because trivia auto-accepts and never reaches Reject, the rejection outcome is exercised by
// constructing a Pending submission, rejecting it in memory, and committing through the real
// DI-managed ApplicationDbContext + MassTransit EF-Core bus outbox.
[Collection(PostgreSqlCollection.Name)]
public sealed class RejectedEvidenceSubmissionPersistenceTests
{
    private static readonly DateTimeOffset ActiveAt = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly string _connectionString;
    private readonly PersistenceTestContextFactory _contextFactory;

    public RejectedEvidenceSubmissionPersistenceTests(PostgreSqlFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    // HU-30 X.3 Gate #1 — a contextually-rejected evidence row round-trips:
    //   ValidationState == Rejected, rejectionReason matches the typed reason, and the row is
    //   committed (not rolled back).
    [Fact]
    public async Task RejectedEvidenceSubmission_RoundTrips_WithPersistedReason()
    {
        var seeded = await SeedActiveTriviaQuestionSessionAsync();

        await using var host = await StartOutboxHostAsync();
        using var scope = host.CreateScope();
        var repository = new LiveSessionRepository(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);

        // Trivia auto-accepts; override to exercise the rejection path.
        var submission = session!.RegisterTriviaAnswer(seeded.TeamId, 1, Guid.NewGuid(), ActiveAt.AddSeconds(5));
        ResetToPending(submission);
        submission.Reject(EvidenceRejectionReason.SubstageBindingMismatch);

        await repository.UpdateAsync(session, CancellationToken.None);

        // Round-trip: fresh context, fresh repository.
        await using var verifyContext = _contextFactory.Create();
        var reloaded = await new LiveSessionRepository(verifyContext)
            .GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        var reloadedSubmission = reloaded!.TriviaAnswerSubmissions.Single();

        reloadedSubmission.ValidationState.Should().Be(EvidenceValidationState.Rejected,
            "the rejected state must be persisted and survive a round-trip");
        reloadedSubmission.RejectionReason.Should().Be(EvidenceRejectionReason.SubstageBindingMismatch,
            "the typed rejection reason must be persisted and read back as the same enum member");
    }

    // HU-30 X.3 Gate #2 — the EvidenceSubmissionRegistered outbox row is inserted atomically
    // with the committed business write (no second publisher stack).
    [Fact]
    public async Task RejectedEvidenceSubmission_InsertsEvidenceSubmissionRegisteredOutboxRow()
    {
        await using var clearContext = _contextFactory.Create();
        await clearContext.Set<OutboxMessage>().ExecuteDeleteAsync();
        await clearContext.Set<OutboxState>().ExecuteDeleteAsync();

        var seeded = await SeedActiveTriviaQuestionSessionAsync();

        await using var host = await StartOutboxHostAsync();
        using var scope = host.CreateScope();
        var repository = new LiveSessionRepository(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);

        var submission = session!.RegisterTriviaAnswer(seeded.TeamId, 1, Guid.NewGuid(), ActiveAt.AddSeconds(5));
        ResetToPending(submission);
        submission.Reject(EvidenceRejectionReason.OutsideSubmissionWindow);

        await repository.UpdateAsync(session, CancellationToken.None);

        // Read the outbox from a fresh connection to prove the row is durably committed.
        await using var verifyContext = _contextFactory.Create();
        var evidenceRows = await verifyContext.Set<OutboxMessage>()
            .Where(message => message.MessageType.Contains(nameof(EvidenceSubmissionRegisteredIntegrationEvent)))
            .ToListAsync();

        evidenceRows.Should().ContainSingle(
            "the EvidenceSubmissionRegistered event must be enqueued as exactly one outbox row");
        evidenceRows.Single().Body.Should().Contain(
            "\"validationState\": 0",
            "the umbrella intake fact stays Pending — the rejection outcome is a separate persistence concern");
    }

    // HU-30 X.3 Gate #3 — all four outbound integration facts land atomically:
    //   EvidenceSubmissionRegistered + Accepted (trivia auto-accept) + AnswerRegistered + Rejected (HU-32 X.2).
    [Fact]
    public async Task RejectedEvidenceSubmission_ProducesFourOutboxMessages()
    {
        await using var clearContext = _contextFactory.Create();
        await clearContext.Set<OutboxMessage>().ExecuteDeleteAsync();
        await clearContext.Set<OutboxState>().ExecuteDeleteAsync();

        var seeded = await SeedActiveTriviaQuestionSessionAsync();

        await using var host = await StartOutboxHostAsync();
        using var scope = host.CreateScope();
        var repository = new LiveSessionRepository(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);

        var submission = session!.RegisterTriviaAnswer(seeded.TeamId, 1, Guid.NewGuid(), ActiveAt.AddSeconds(5));
        ResetToPending(submission);
        submission.Reject(EvidenceRejectionReason.UnauthorizedOrigin);

        await repository.UpdateAsync(session, CancellationToken.None);

        await using var verifyContext = _contextFactory.Create();
        var allOutboxRows = await verifyContext.Set<OutboxMessage>().ToListAsync();

        allOutboxRows.Should().HaveCount(4,
            "trivia auto-accept + manual reject fills all four outbox publishers: " +
            "EvidenceSubmissionRegistered, EvidenceSubmissionAccepted, AnswerRegistered, EvidenceSubmissionRejected");

        allOutboxRows.Should().ContainSingle(m =>
                m.MessageType.Contains(nameof(EvidenceSubmissionRegisteredIntegrationEvent)),
            "the umbrella EvidenceSubmissionRegistered fact fires regardless of acceptance/rejection outcome");

        allOutboxRows.Should().ContainSingle(m =>
                m.MessageType.Contains(nameof(EvidenceSubmissionAcceptedIntegrationEvent)),
            "the trivia auto-accept path raises EvidenceSubmissionAccepted before the test resets to Pending");

        allOutboxRows.Should().ContainSingle(m =>
                m.MessageType.Contains(nameof(AnswerRegisteredIntegrationEvent)),
            "the specialized AnswerRegistered fact still fires for the trivia context");

        allOutboxRows.Should().ContainSingle(m =>
                m.MessageType.Contains(nameof(EvidenceSubmissionRejectedIntegrationEvent)),
            "the HU-32 X.2 EvidenceSubmissionRejected fact fires for the manual rejection path");
    }

    // Resets a TriviaAnswerSubmission back to Pending so the Reject transition path is exercisable.
    // Trivia auto-accepts and never reaches Reject (HU-34), so the rejection outcome must be
    // constructed in the integration test itself.
    private static void ResetToPending(EvidenceSubmission submission)
    {
        typeof(EvidenceSubmission)
            .GetProperty(nameof(EvidenceSubmission.ValidationState),
                BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(submission, EvidenceValidationState.Pending);
    }

    private ServiceProvider BuildOutboxProvider()
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
            "Rejection Persistence Trivia Session",
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
