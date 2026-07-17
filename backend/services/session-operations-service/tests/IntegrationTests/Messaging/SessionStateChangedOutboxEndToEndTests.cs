using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.IntegrationTests.Persistence;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Interceptors;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Messaging;

[Collection(PostgreSqlCollection.Name)]
public sealed class SessionStateChangedOutboxEndToEndTests
{
    private static readonly TimeSpan PromptBudget = TimeSpan.FromSeconds(2);
    private static readonly Guid OperatorExternalId = Guid.Parse("06cdd74a-c80e-4f8b-9788-a0eef85d05f2");

    private readonly string _connectionString;
    private readonly PersistenceTestContextFactory _contextFactory;

    public SessionStateChangedOutboxEndToEndTests(PostgreSqlFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task Transition_WhenBrokerUnavailable_CommitsAndRetainsPendingOutboxRow()
    {
        var seeded = await SeedScheduledSessionAsync();

        await using var host = StartUnreachableBrokerHost();
        using var scope = host.CreateScope();
        var repository = new LiveSessionRepository(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        var occurredAt = DateTimeOffset.UtcNow;
        session!.MoveTo(
            SessionState.Preparing,
            occurredAt,
            new SessionStateTransitionPolicy(),
            "Broker down transition",
            seeded.OperatorUserId);

        var stopwatch = Stopwatch.StartNew();
        await repository.UpdateAsync(session, CancellationToken.None);
        stopwatch.Stop();

        await AssertTransitionCommittedAsync(seeded.LiveSessionId, seeded.OperatorUserId);
        stopwatch.Elapsed.Should().BeLessThan(
            PromptBudget,
            "a down broker must not stall the transition save — the outbox publish is a local insert");

        await using var verifyContext = _contextFactory.Create();
        var pending = await verifyContext.Set<OutboxMessage>()
            .Where(message => message.MessageType.Contains(nameof(SessionStateChangedIntegrationEvent)))
            .ToListAsync();
        pending.Should().ContainSingle(
            "with the broker down the SessionStateChanged fact must survive as a pending outbox row");
    }

    [Fact]
    public async Task Transition_EnqueuesSessionStateChanged_DeliversToBoundConsumer()
    {
        var rabbit = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();
        await DockerAvailability.StartOrSkipAsync(() => rabbit.StartAsync(), rabbit.DisposeAsync);

        try
        {
            var seeded = await SeedScheduledSessionAsync();

            await CaptureTransitionAsPendingOutboxRowAsync(rabbit, seeded);

            await using (var verifyContext = _contextFactory.Create())
            {
                var pending = await verifyContext.Set<OutboxMessage>()
                    .Where(message => message.MessageType.Contains(nameof(SessionStateChangedIntegrationEvent)))
                    .ToListAsync();
                pending.Should().ContainSingle(
                    "phase 1 must capture the SessionStateChanged fact as a pending outbox row");
            }

            var probe = new SessionStateChangedProbe();
            using var host = BuildRecoveryHost(rabbit, probe);
            await host.StartAsync();
            try
            {
                var received = await probe.Received.Task.WaitAsync(TimeSpan.FromSeconds(30));

                received.LiveSessionId.Should().Be(seeded.LiveSessionId);
                received.PreviousState.Should().Be(SessionState.Scheduled);
                received.CurrentState.Should().Be(SessionState.Preparing);
                received.ResponsibleUserExternalId.Should().Be(OperatorExternalId);
                received.Reason.Should().Be("Phase 1 transition");

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

    private async Task CaptureTransitionAsPendingOutboxRowAsync(RabbitMqContainer rabbit, SeededSession seeded)
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
            session!.MoveTo(
                SessionState.Preparing,
                DateTimeOffset.UtcNow,
                new SessionStateTransitionPolicy(),
                "Phase 1 transition",
                seeded.OperatorUserId,
                OperatorExternalId);
            await repository.UpdateAsync(session, CancellationToken.None);
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    private IHost BuildRecoveryHost(RabbitMqContainer rabbit, SessionStateChangedProbe probe)
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

                    bus.AddConsumer<SessionStateChangedTestConsumer>();
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
                .CountAsync(message => message.MessageType.Contains(nameof(SessionStateChangedIntegrationEvent)));
            if (remaining == 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        await using var finalContext = _contextFactory.Create();
        var stillPending = await finalContext.Set<OutboxMessage>()
            .CountAsync(message => message.MessageType.Contains(nameof(SessionStateChangedIntegrationEvent)));
        stillPending.Should().Be(0, "the delivery service must remove the outbox row once it is delivered to the broker");
    }

    private async Task AssertTransitionCommittedAsync(Guid liveSessionId, int operatorUserId)
    {
        await using var assertContext = _contextFactory.Create();
        var reloaded = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSessionId, CancellationToken.None);
        reloaded!.State.Should().Be(SessionState.Preparing, "the transition must commit even when the broker is unreachable");
        reloaded.SessionEvents.Should().ContainSingle();
        reloaded.SessionEvents.Single().ActorType.Should().Be(SessionEventActorType.Operator);
        reloaded.SessionEvents.Single().ActorId.Should().Be(operatorUserId);
    }

    private UnreachableBrokerHost StartUnreachableBrokerHost()
    {
        var deadPort = ReserveDeadLocalPort();

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

        var startTask = bus.StartAsync(CancellationToken.None);
        _ = startTask.ContinueWith(t => _ = t.Exception, TaskScheduler.Default);

        return new UnreachableBrokerHost(provider, bus);
    }

    private static int ReserveDeadLocalPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class UnreachableBrokerHost : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IBusControl _bus;

        public UnreachableBrokerHost(ServiceProvider provider, IBusControl bus)
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
            }

            await _provider.DisposeAsync();
        }
    }

    private sealed class SessionStateChangedProbe
    {
        public TaskCompletionSource<SessionStateChangedIntegrationEvent> Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class SessionStateChangedTestConsumer : IConsumer<SessionStateChangedIntegrationEvent>
    {
        private readonly SessionStateChangedProbe _probe;

        public SessionStateChangedTestConsumer(SessionStateChangedProbe probe) => _probe = probe;

        public Task Consume(ConsumeContext<SessionStateChangedIntegrationEvent> context)
        {
            _probe.Received.TrySetResult(context.Message);
            return Task.CompletedTask;
        }
    }

    private async Task<SeededSession> SeedScheduledSessionAsync()
    {
        await using var resetContext = _contextFactory.Create();
        await resetContext.LiveSessions.ExecuteDeleteAsync();
        await resetContext.Set<OutboxMessage>().ExecuteDeleteAsync();
        await resetContext.Set<OutboxState>().ExecuteDeleteAsync();

        const int operatorUserId = 99;
        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Outbox Transition Session",
            45,
            DateTimeOffset.UtcNow.AddHours(2),
            CreateTreasureHuntSnapshot(sourceMissionId));

        session.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);
        session.AssignOperator(operatorUserId, DateTimeOffset.UtcNow.AddMinutes(-10));

        await using var seedContext = _contextFactory.Create();
        seedContext.LiveSessions.Add(session);
        await seedContext.SaveChangesAsync(CancellationToken.None);

        return new SeededSession(session.LiveSessionId, operatorUserId);
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(Guid sourceMissionId)
    {
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Seeded Mission",
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
            []);
    }

    private sealed record SeededSession(Guid LiveSessionId, int OperatorUserId);
}
