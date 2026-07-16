using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Api.Hubs;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

/// <summary>
/// Pins the real-time side of the authoritative session timer broadcast: a SessionTimerUpdated
/// message carrying the treasure-hunt remaining reaches the live-session:{id} group, proving the
/// broadcast transport works for the treasure-hunt substage window.
/// </summary>
[Collection(PostgreSqlCollection.Name)]
public sealed class SessionTimerBroadcastHubTests : IAsyncLifetime
{
    private const int OperatorUserId = 77;

    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;

    public SessionTimerBroadcastHubTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new SessionOperationsApiWebApplicationFactory(_fixture.ConnectionString);
        _factory.EligibleTeamsClient.IsEligible = true;
        _factory.AuthenticatedActorProfileAccessClient.CurrentActor = new AuthenticatedActorProfileLookupDto(
            OperatorUserId,
            "kc-operator-77",
            "Operator",
            true);
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task TimerBroadcaster_BroadcastsTreasureHuntSessionTimerUpdatedToConnectedParticipant()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedActiveTreasureHuntSessionWithDisconnectedParticipantAsync(externalIdentityId);

        await using var participant = CreateHubConnection(externalIdentityId.ToString(), "Participant", "participant@example.com");
        await participant.StartAsync();

        await participant.InvokeAsync(
            nameof(SessionsHub.ReconnectAsync),
            seeded.LiveSessionId,
            new SessionsHub.ReconnectParticipantHubRequest(seeded.TeamId, "Nova", null));

        var received = new TaskCompletionSource<SessionTimerUpdatedNotificationDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        participant.On<SessionTimerUpdatedNotificationDto>(
            SignalRSessionTimerBroadcaster.TimerUpdatedMethod,
            notification => received.TrySetResult(notification));

        var emittedAt = DateTimeOffset.UtcNow;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
            var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
            var snapshot = session!.GetAuthoritativeSessionTimerSnapshot(emittedAt);

            var broadcast = new SessionTimerUpdatedNotificationDto(
                seeded.LiveSessionId,
                RemainingMilliseconds: (long)Math.Ceiling(snapshot.RemainingDuration.TotalMilliseconds),
                IsPaused: session.State == SessionState.Paused,
                EmittedAt: emittedAt,
                TotalMilliseconds: (long)Math.Ceiling(snapshot.TotalDuration.TotalMilliseconds),
                IsExpired: snapshot.IsExpired,
                SessionState: session.State.ToString());

            var broadcaster = scope.ServiceProvider.GetRequiredService<ISessionTimerBroadcaster>();
            await broadcaster.BroadcastTimerUpdatedAsync(broadcast, CancellationToken.None);
        }

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(received.Task, "timer updates must broadcast over SignalR");

        var notification = await received.Task;
        notification.LiveSessionId.Should().Be(seeded.LiveSessionId);
        notification.IsPaused.Should().BeFalse();
        notification.IsExpired.Should().BeFalse();
        notification.SessionState.Should().Be(nameof(SessionState.Active));
        notification.EmittedAt.Should().BeCloseTo(emittedAt, TimeSpan.FromSeconds(1));
        notification.RemainingMilliseconds.Should().BeGreaterThan(0);
        notification.TotalMilliseconds.Should().Be(2_700_000);
    }

    [Fact]
    public async Task TimerBroadcaster_BroadcastsTreasureHuntExpiredToConnectedParticipant()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedExpiredTreasureHuntSessionWithDisconnectedParticipantAsync(externalIdentityId);

        await using var participant = CreateHubConnection(externalIdentityId.ToString(), "Participant", "participant@example.com");
        await participant.StartAsync();

        await participant.InvokeAsync(
            nameof(SessionsHub.ReconnectAsync),
            seeded.LiveSessionId,
            new SessionsHub.ReconnectParticipantHubRequest(seeded.TeamId, "Nova", null));

        var received = new TaskCompletionSource<SessionTimerUpdatedNotificationDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        participant.On<SessionTimerUpdatedNotificationDto>(
            SignalRSessionTimerBroadcaster.TimerUpdatedMethod,
            notification => received.TrySetResult(notification));

        var emittedAt = DateTimeOffset.UtcNow;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
            var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
            var snapshot = session!.MarkSubstageTimerExpiredIfElapsed(emittedAt);

            var broadcast = new SessionTimerUpdatedNotificationDto(
                seeded.LiveSessionId,
                RemainingMilliseconds: (long)Math.Ceiling(snapshot.RemainingDuration.TotalMilliseconds),
                IsPaused: session.State == SessionState.Paused,
                EmittedAt: emittedAt,
                TotalMilliseconds: (long)Math.Ceiling(snapshot.TotalDuration.TotalMilliseconds),
                IsExpired: snapshot.IsExpired,
                SessionState: session.State.ToString());

            var broadcaster = scope.ServiceProvider.GetRequiredService<ISessionTimerBroadcaster>();
            await broadcaster.BroadcastTimerUpdatedAsync(broadcast, CancellationToken.None);
        }

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(received.Task, "timer updates must broadcast over SignalR");

        var notification = await received.Task;
        notification.LiveSessionId.Should().Be(seeded.LiveSessionId);
        notification.IsExpired.Should().BeTrue();
        notification.RemainingMilliseconds.Should().Be(0);
        notification.TotalMilliseconds.Should().Be(60_000);
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

    private async Task<SeededSession> SeedActiveTreasureHuntSessionWithDisconnectedParticipantAsync(Guid externalIdentityId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);

        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Treasure Hunt Timer Broadcast",
            45,
            createdAt,
            CreateTreasureHuntSnapshot(sourceMissionId));
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

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(session.LiveSessionId, team.TeamId);
    }

    private async Task<SeededSession> SeedExpiredTreasureHuntSessionWithDisconnectedParticipantAsync(Guid externalIdentityId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);

        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Expired Treasure Hunt Timer",
            1,
            createdAt,
            CreateTreasureHuntSnapshot(sourceMissionId));
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

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(session.LiveSessionId, team.TeamId);
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

    private sealed record SeededSession(Guid LiveSessionId, Guid TeamId);
}
