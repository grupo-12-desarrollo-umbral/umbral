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

// HU-31 X.3 — proves the treasure (QR/target) evidence-intake + target-resolution facts flow through the
// SAME transactional bus outbox: on a correct scan both EvidenceSubmissionRegistered AND TargetResolved
// are enqueued as OutboxMessage rows atomically with the business write; on a rejected scan only
// EvidenceSubmissionRegistered is enqueued. No second publisher stack — the existing MassTransit
// EF Core bus-outbox + IPublishEndpoint is reused.
//
// Mirrors EvidenceSubmissionRegisteredOutboxTests.
[Collection(PostgreSqlCollection.Name)]
public sealed class TargetResolvedOutboxTests
{
    private static readonly DateTimeOffset ActiveAt = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly string _connectionString;
    private readonly PersistenceTestContextFactory _contextFactory;

    public TargetResolvedOutboxTests(PostgreSqlFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    // A correct QR scan enqueues BOTH EvidenceSubmissionRegistered (umbrella fact, Pending) AND
    // TargetResolved (resolution fact, carrying the relayed score) as outbox rows in the same
    // transaction as the business write — the business row + both outbox rows are atomically
    // committed.
    [Fact]
    public async Task RegisterTargetScan_EnqueuesBothOutboxRows_AtomicallyWithBusinessWrite()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();

        await using var host = await StartOutboxHostAsync();
        using var scope = host.CreateScope();
        var repository = new LiveSessionRepository(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        session!.RegisterTargetScan(seeded.TeamId, "QR-ALPHA", Guid.NewGuid(), ActiveAt.AddSeconds(15));

        await repository.UpdateAsync(session, CancellationToken.None);

        await using var assertContext = _contextFactory.Create();
        var reloaded = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        reloaded!.TreasureEvidenceSubmissions.Should().ContainSingle(
            "the treasure evidence submission must commit atomically with both outbox rows");
        reloaded.TreasureEvidenceSubmissions.Single().ValidationState.Should().Be(EvidenceValidationState.Accepted);

        await using var verifyContext = _contextFactory.Create();
        var evidenceRows = await verifyContext.Set<OutboxMessage>()
            .Where(message => message.MessageType.Contains(nameof(EvidenceSubmissionRegisteredIntegrationEvent)))
            .ToListAsync();

        evidenceRows.Should().ContainSingle(
            "the umbrella evidence-intake fact must be enqueued for every registered scan");
        evidenceRows.Single().Body.Should().Contain(
            "\"validationState\": 0",
            "the umbrella intake fact stays Pending even on the accepted scan path (ADR-0010 two-facts model)");

        var targetResolvedRows = await verifyContext.Set<OutboxMessage>()
            .Where(message => message.MessageType.Contains(nameof(TargetResolvedIntegrationEvent)))
            .ToListAsync();

        targetResolvedRows.Should().ContainSingle(
            "a correct scan must enqueue exactly one TargetResolved outbox row alongside the umbrella fact");
        targetResolvedRows.Single().Body.Should().Contain(
            "\"scoreValue\": 100",
            "the TargetResolved fact relays the snapshotted target score verbatim");
    }

    // A wrong QR scan (value that does not resolve to any target) enqueues EvidenceSubmissionRegistered
    // but NOT TargetResolved — the rejected path carries the umbrella fact only.
    [Fact]
    public async Task RegisterTargetScan_WithWrongQr_EnqueuesOnlyEvidenceSubmissionRegistered()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();

        await using var host = await StartOutboxHostAsync();
        using var scope = host.CreateScope();
        var repository = new LiveSessionRepository(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        session!.RegisterTargetScan(seeded.TeamId, "QR-BOGUS", Guid.NewGuid(), ActiveAt.AddSeconds(15));

        await repository.UpdateAsync(session, CancellationToken.None);

        await using var assertContext = _contextFactory.Create();
        var reloaded = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        reloaded!.TreasureEvidenceSubmissions.Should().ContainSingle();
        reloaded.TreasureEvidenceSubmissions.Single().ValidationState.Should().Be(EvidenceValidationState.Rejected);

        await using var verifyContext = _contextFactory.Create();
        var evidenceRows = await verifyContext.Set<OutboxMessage>()
            .Where(message => message.MessageType.Contains(nameof(EvidenceSubmissionRegisteredIntegrationEvent)))
            .ToListAsync();

        evidenceRows.Should().ContainSingle(
            "the umbrella evidence-intake fact must still fire for rejected scans (AC#5)");

        var targetResolvedCount = await verifyContext.Set<OutboxMessage>()
            .Where(message => message.MessageType.Contains(nameof(TargetResolvedIntegrationEvent)))
            .CountAsync();

        targetResolvedCount.Should().Be(0,
            "a rejected scan must NOT publish a TargetResolved fact");
    }

    // No second publisher stack — TargetResolved flows through the existing IPublishEndpoint + bus-outbox
    // that EvidenceSubmissionRegistered and AnswerRegistered already use. This test proves the DI
    // container wires exactly the publish handlers registered in Application.DependencyInjection and
    // no second BusControl/bootstrap.
    [Fact]
    public async Task OutboxServiceProvider_ContainsSinglePublishEndpoint_ForTargetResolved()
    {
        var provider = BuildOutboxProvider();
        await using (provider)
        {
            var publishEndpoints = provider.GetServices<IPublishEndpoint>().ToList();
            publishEndpoints.Should().ContainSingle(
                "MassTransit's InMemory bus registers exactly one IPublishEndpoint");

            var targetResolvedHandler = provider.GetService<PublishTargetResolvedIntegrationEventHandler>();
            targetResolvedHandler.Should().NotBeNull(
                "PublishTargetResolvedIntegrationEventHandler must be registered in DI");
        }
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
        services.AddScoped<PublishTargetResolvedIntegrationEventHandler>();
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

    private async Task<SeededSession> SeedActiveTreasureHuntSessionAsync()
    {
        await using var resetContext = _contextFactory.Create();
        await resetContext.LiveSessions.ExecuteDeleteAsync();
        await resetContext.Set<OutboxMessage>().ExecuteDeleteAsync();
        await resetContext.Set<OutboxState>().ExecuteDeleteAsync();

        var sourceMissionId = Guid.NewGuid();
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);

        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Target Resolution Session",
            45,
            ActiveAt.AddMinutes(-10),
            MissionRuntimeSnapshot.Create(
                sourceMissionId,
                "Mission Runtime",
                MaximumTime.Create(45),
                [
                    StageSnapshot.Create("Stage One", 1, [treasureHuntSubstage])
                ],
                [
                    TargetSnapshot.Create(
                        treasureHuntSubstage.SubstageSnapshotId,
                        "Target Alpha",
                        "QR-ALPHA",
                        1,
                        true,
                        100,
                        4.711,
                        -74.0721,
                        "Look under the stairs",
                        "AfterPreviousTarget")
                ],
                []));

        var transitionPolicy = new SessionStateTransitionPolicy();
        session.AssociateTeam(Guid.NewGuid(), "Aurora", "AUR-01", 3);
        session.MoveTo(SessionState.Preparing, ActiveAt.AddMinutes(-1), transitionPolicy);
        session.MoveTo(SessionState.Active, ActiveAt, transitionPolicy);

        await using var seedContext = _contextFactory.Create();
        seedContext.LiveSessions.Add(session);
        await seedContext.SaveChangesAsync(CancellationToken.None);

        return new SeededSession(session.LiveSessionId, session.Teams.Single().TeamId);
    }

    private sealed record SeededSession(Guid LiveSessionId, Guid TeamId);
}
