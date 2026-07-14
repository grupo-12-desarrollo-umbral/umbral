using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.IntegrationTests.Persistence;
using umbral_backend.Infrastructure.Messaging.Consumers;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Interceptors;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Messaging;

// HU-32 X.3 — proves evidence trace entries reach terminal state through the broker consumer path.
// Phase 1 enqueues three outbox rows (registered → accepted → rejected) for the SAME submission.
// Phase 2 starts a recovery host with the real trace consumers. The submission is consumed in
// outbox order, so the first resolution (accepted) should set the terminal state.
[Collection(PostgreSqlCollection.Name)]
public sealed class EvidenceTraceBrokerDeliveryE2ETests
{
    private static readonly DateTimeOffset ActiveAt = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly string _connectionString;
    private readonly PersistenceTestContextFactory _contextFactory;

    public EvidenceTraceBrokerDeliveryE2ETests(PostgreSqlFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    // A QR scan that the contextual validation chain rejects (e.g. OutsideSubmissionWindow) reaches
    // terminal Rejected state through the consumer path. Only two facts fire: Registered + Rejected
    // (no auto-accept — contextual rejects never reach the concrete form's resolution path).
    [Fact]
    public async Task RejectedEvidence_ReachesTraceTerminalState_AfterOutboxDrains()
    {
        var rabbit = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();
        await DockerAvailability.StartOrSkipAsync(() => rabbit.StartAsync(), rabbit.DisposeAsync);

        try
        {
            var seeded = await SeedActiveTreasureHuntSessionAsync();

            var evidenceId = await CaptureRejectedEvidenceAsPendingOutboxRowsAsync(rabbit, seeded);

            using var host = BuildRecoveryHost(rabbit);
            await host.StartAsync();
            try
            {
                var entry = await PollTraceEntryUntilAsync(evidenceId,
                    e => e.ValidationState == EvidenceValidationState.Rejected);

                entry.ValidationState.Should().Be(EvidenceValidationState.Rejected);
                entry.RejectionReason.Should().Be("The scanned value does not resolve to a target.");
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

    private async Task<Guid> CaptureRejectedEvidenceAsPendingOutboxRowsAsync(
        RabbitMqContainer rabbit, SeededSession seeded)
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
            var teamId = session!.Teams.Single().TeamId;
            var activeSubstageId = session.ActiveSubstageId!.Value;

            // HU-31: RegisterTargetScan resolves the scanned value against the snapshot. A scan that
            // does not match any target in the active substage is rejected with the reason
            // ScannedValueDoesNotResolveToTarget. This path fires Registered → Rejected (no accept).
            var submission = session.RegisterTargetScan(teamId, "UNKNOWN_QR", Guid.NewGuid(), ActiveAt.AddSeconds(5));

            await repository.UpdateAsync(session, CancellationToken.None);

            await bus.StopAsync();
            return submission.EvidenceSubmissionId;
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    private IHost BuildRecoveryHost(RabbitMqContainer rabbit)
    {
        return new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(_connectionString));

                services.AddScoped<IEvidenceTraceRepository, EvidenceTraceRepository>();

                services.AddTransient<
                    Application.Sessions.Commands.RecordEvidenceTraceRegistration.RecordEvidenceTraceRegistrationCommandHandler>();
                services.AddTransient<
                    Application.Sessions.Commands.RecordEvidenceTraceResolution.RecordEvidenceTraceResolutionCommandHandler>();

                services.AddTransient<ISender>(sp =>
                {
                    var registrationHandler = sp.GetRequiredService<
                        Application.Sessions.Commands.RecordEvidenceTraceRegistration.RecordEvidenceTraceRegistrationCommandHandler>();
                    var resolutionHandler = sp.GetRequiredService<
                        Application.Sessions.Commands.RecordEvidenceTraceResolution.RecordEvidenceTraceResolutionCommandHandler>();
                    return new TraceCommandSender(registrationHandler, resolutionHandler);
                });

                services.AddMassTransit(bus =>
                {
                    bus.AddEntityFrameworkOutbox<ApplicationDbContext>(outbox =>
                    {
                        outbox.UsePostgres();
                        outbox.UseBusOutbox();
                        outbox.QueryDelay = TimeSpan.FromSeconds(1);
                    });

                    bus.AddConsumer<EvidenceSubmissionRegisteredConsumer>();
                    bus.AddConsumer<EvidenceSubmissionAcceptedConsumer>();
                    bus.AddConsumer<EvidenceSubmissionRejectedConsumer>();
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

    private async Task<EvidenceTraceEntry> PollTraceEntryUntilAsync(
        Guid evidenceSubmissionId,
        Func<EvidenceTraceEntry, bool> predicate)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await using var context = _contextFactory.Create();
            var entry = await context.Set<EvidenceTraceEntry>()
                .SingleOrDefaultAsync(e => e.EvidenceSubmissionId == evidenceSubmissionId);
            if (entry is not null && predicate(entry))
            {
                return entry;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        await using var finalContext = _contextFactory.Create();
        var final = await finalContext.Set<EvidenceTraceEntry>()
            .SingleOrDefaultAsync(e => e.EvidenceSubmissionId == evidenceSubmissionId);
        final.Should().NotBeNull("the trace entry must exist after outbox drains");
        final!.Should().Match<EvidenceTraceEntry>(e => predicate(e),
            "the trace entry should reach the expected terminal state after outbox drains");
        return final;
    }

    private async Task<SeededSession> SeedActiveTreasureHuntSessionAsync()
    {
        await using var resetContext = _contextFactory.Create();
        await resetContext.LiveSessions.ExecuteDeleteAsync();
        await resetContext.Set<OutboxMessage>().ExecuteDeleteAsync();
        await resetContext.Set<OutboxState>().ExecuteDeleteAsync();
        await resetContext.Set<EvidenceTraceEntry>().ExecuteDeleteAsync();

        var sourceMissionId = Guid.NewGuid();
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);
        var runtimeSnapshot = MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Mission Runtime",
            MaximumTime.Create(20),
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
                    "VisibleWhenSubstageStarts")
            ],
            []);

        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Trace E2E Treasure Hunt Session",
            20,
            ActiveAt.AddMinutes(-10),
            runtimeSnapshot);

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

/// <summary>
/// Lightweight ISender that routes trace commands to the two trace-write handlers.
/// </summary>
internal sealed class TraceCommandSender : ISender
{
    private readonly IRequestHandler<
        umbral_backend.Application.Sessions.Commands.RecordEvidenceTraceRegistration.RecordEvidenceTraceRegistrationCommand> _registrationHandler;
    private readonly IRequestHandler<
        umbral_backend.Application.Sessions.Commands.RecordEvidenceTraceResolution.RecordEvidenceTraceResolutionCommand> _resolutionHandler;

    public TraceCommandSender(
        IRequestHandler<umbral_backend.Application.Sessions.Commands.RecordEvidenceTraceRegistration.RecordEvidenceTraceRegistrationCommand> registrationHandler,
        IRequestHandler<umbral_backend.Application.Sessions.Commands.RecordEvidenceTraceResolution.RecordEvidenceTraceResolutionCommand> resolutionHandler)
    {
        _registrationHandler = registrationHandler;
        _resolutionHandler = resolutionHandler;
    }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        return request switch
        {
            umbral_backend.Application.Sessions.Commands.RecordEvidenceTraceRegistration.RecordEvidenceTraceRegistrationCommand cmd =>
                _registrationHandler.Handle(cmd, cancellationToken),
            umbral_backend.Application.Sessions.Commands.RecordEvidenceTraceResolution.RecordEvidenceTraceResolutionCommand cmd =>
                _resolutionHandler.Handle(cmd, cancellationToken),
            _ => throw new NotSupportedException($"Unknown request type: {typeof(TRequest).Name}")
        };
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}
