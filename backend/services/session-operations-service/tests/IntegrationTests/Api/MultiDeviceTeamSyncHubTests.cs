using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using umbral_backend.Api.Hubs;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Sessions;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

// HU-08 — the one genuine deliverable: an end-to-end, multi-device regression lock asserting
// AC1–AC4 together through the real SignalR hub (WebApplicationFactory test host). This behaviour
// is emergent from three shipped slices (HU-07B reconnect/presence/ConnectionTracker,
// HU-23 team-board broadcast); no new production type is added. A future refactor of the hub,
// broadcaster, or connection tracker that silently breaks multi-device sync must fail here.
//
// Every wait is bounded: message receipt uses a TaskCompletionSource raced against a timeout
// (never an unbounded await), and the AC4 negative case asserts "did NOT arrive within the
// window" — so no assertion can hang if a message never comes.
[Collection(PostgreSqlCollection.Name)]
public sealed class MultiDeviceTeamSyncHubTests : IAsyncLifetime
{
    private static readonly TimeSpan MessageArrivalTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan NegativeObservationWindow = TimeSpan.FromSeconds(2);

    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;

    public MultiDeviceTeamSyncHubTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new SessionOperationsApiWebApplicationFactory(_fixture.ConnectionString);
        _factory.EligibleTeamsClient.IsEligible = true;
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task MultiDeviceTeamSync_DeliversToEveryDeviceOfTheTeam_IsolatesOtherTeams_AndKeepsPresenceUntilLastDevice()
    {
        var primaryIdentityId = Guid.NewGuid();
        var secondaryIdentityId = Guid.NewGuid();
        var seeded = await SeedActiveTwoTeamSessionAsync(primaryIdentityId, secondaryIdentityId);

        // Two devices for the SAME participant/team, plus one device on a DIFFERENT team.
        await using var deviceA = CreateHubConnection(primaryIdentityId.ToString(), "Participant", "alpha@example.com");
        await using var deviceB = CreateHubConnection(primaryIdentityId.ToString(), "Participant", "alpha@example.com");
        await using var otherTeamDevice = CreateHubConnection(secondaryIdentityId.ToString(), "Participant", "bravo@example.com");

        await deviceA.StartAsync();
        await deviceB.StartAsync();
        await otherTeamDevice.StartAsync();

        // Register bounded receivers BEFORE reconnecting so no team-board push can be missed.
        var deviceABoard = BoundedReceiver(deviceA);
        var deviceBBoard = BoundedReceiver(deviceB);
        var otherTeamBoard = BoundedReceiver(otherTeamDevice);

        // AC3 — a fresh/reconnecting connection is admitted idempotently and carries current
        // SessionState + timer for hydration. All three connections reconnect into the live session.
        var payloadA = await ReconnectAsync(deviceA, seeded.LiveSessionId, seeded.PrimaryTeamId);
        var payloadB = await ReconnectAsync(deviceB, seeded.LiveSessionId, seeded.PrimaryTeamId);
        var payloadOther = await ReconnectAsync(otherTeamDevice, seeded.LiveSessionId, seeded.SecondaryTeamId);

        payloadA.IsReconnect.Should().BeTrue();
        payloadB.IsReconnect.Should().BeTrue();
        // Both devices are the same participant — idempotent admission returns the same identity.
        payloadB.SessionParticipantId.Should().Be(payloadA.SessionParticipantId);
        payloadA.SessionState.Should().Be(nameof(SessionState.Active));
        payloadA.Timer.Should().NotBeNull("the reconnect result hydrates the device with the current session timer (AC3)");
        payloadA.Timer!.LiveSessionId.Should().Be(seeded.LiveSessionId);
        payloadOther.SessionParticipantId.Should().NotBe(payloadA.SessionParticipantId);

        // AC1/AC2 — a single team-board change reaches EVERY device of that participant/team.
        var broadcaster = _factory.Services.GetRequiredService<ITeamBoardBroadcaster>();
        var board = CreateBoardDto(seeded.LiveSessionId, seeded.PrimaryTeamId);
        await broadcaster.BroadcastTeamBoardUpdatedAsync(board, CancellationToken.None);

        var receivedA = await deviceABoard.WaitAsync(MessageArrivalTimeout);
        var receivedB = await deviceBBoard.WaitAsync(MessageArrivalTimeout);
        receivedA.Should().NotBeNull("device A of the team must receive TeamBoardUpdated (AC1/AC2)");
        receivedB.Should().NotBeNull("device B of the same team must also receive TeamBoardUpdated (AC1/AC2)");
        receivedA!.TeamId.Should().Be(seeded.PrimaryTeamId);
        receivedB!.TeamId.Should().Be(seeded.PrimaryTeamId);

        // AC4 — a device on a DIFFERENT team never receives the first team's board. Bounded negative
        // wait: we assert nothing arrived within the observation window, never wait for a message.
        var receivedOther = await otherTeamBoard.WaitAsync(NegativeObservationWindow);
        receivedOther.Should().BeNull("a device on another team must never receive this team's board (AC4)");

        // Last-device presence — dropping ONE of the participant's two devices must NOT disconnect
        // the participant; presence survives until the last connection drops.
        await deviceA.StopAsync();
        var whileDeviceBRemains = await WaitForParticipantStateAsync(
            seeded.LiveSessionId,
            payloadA.SessionParticipantId,
            expectedDisconnected: false);
        whileDeviceBRemains.IsDisconnected.Should().BeFalse(
            "the participant stays present while a second device is still connected");

        await deviceB.StopAsync();
        var afterLastDevice = await WaitForParticipantStateAsync(
            seeded.LiveSessionId,
            payloadA.SessionParticipantId,
            expectedDisconnected: true);
        afterLastDevice.IsDisconnected.Should().BeTrue(
            "only the last device dropping disconnects the participant");
    }

    private static BoundedBoardReceiver BoundedReceiver(HubConnection connection)
    {
        var completion = new TaskCompletionSource<ParticipantTeamBoardDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<ParticipantTeamBoardDto>(
            SignalRTeamBoardBroadcaster.TeamBoardUpdatedMethod,
            payload => completion.TrySetResult(payload));
        return new BoundedBoardReceiver(completion.Task);
    }

    private static async Task<ReconnectParticipantResultDto> ReconnectAsync(
        HubConnection connection,
        Guid liveSessionId,
        Guid teamId)
    {
        return await connection.InvokeAsync<ReconnectParticipantResultDto>(
            nameof(SessionsHub.ReconnectAsync),
            liveSessionId,
            new SessionsHub.ReconnectParticipantHubRequest(teamId, "Nova", null));
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

    private async Task<SeededSession> SeedActiveTwoTeamSessionAsync(
        Guid primaryIdentityId,
        Guid secondaryIdentityId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTimeOffset.UtcNow;
        var createdAt = now.AddMinutes(-20);
        var sourceMissionId = Guid.NewGuid();

        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"MD-{Guid.NewGuid():N}"[..12],
            "Multi-Device Team Sync",
            maximumTimeMinutes: 45,
            createdAt,
            CreateTreasureHuntSnapshot(sourceMissionId));

        var primaryTeam = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var secondaryTeam = session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-01", 4);

        var primaryParticipant = session.AdmitParticipant(
            primaryIdentityId,
            "AlphaUser",
            primaryTeam.TeamId,
            createdAt.AddMinutes(1),
            new JoinPolicy()).Participant;
        var secondaryParticipant = session.AdmitParticipant(
            secondaryIdentityId,
            "BravoUser",
            secondaryTeam.TeamId,
            createdAt.AddMinutes(1),
            new JoinPolicy()).Participant;

        // Disconnect both so each device reconnects through the hub (the multi-device path).
        session.DisconnectParticipant(primaryParticipant.SessionParticipantId, createdAt.AddMinutes(2));
        session.DisconnectParticipant(secondaryParticipant.SessionParticipantId, createdAt.AddMinutes(2));

        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(1), policy);
        session.MoveTo(SessionState.Active, now, policy);

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(session.LiveSessionId, primaryTeam.TeamId, secondaryTeam.TeamId);
    }

    private async Task<SessionParticipant> WaitForParticipantStateAsync(
        Guid liveSessionId,
        Guid sessionParticipantId,
        bool expectedDisconnected)
    {
        const int maxAttempts = 20;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var participant = await dbContext.LiveSessions
                .AsNoTracking()
                .Include(session => session.Participants)
                .Where(session => session.LiveSessionId == liveSessionId)
                .SelectMany(session => session.Participants)
                .SingleAsync(candidate => candidate.SessionParticipantId == sessionParticipantId);

            if (participant.IsDisconnected == expectedDisconnected)
            {
                return participant;
            }

            await Task.Delay(100);
        }

        throw new Xunit.Sdk.XunitException(
            $"Participant {sessionParticipantId} did not reach disconnected={expectedDisconnected}.");
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(Guid sourceMissionId)
    {
        var substage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);
        var target = TargetSnapshot.Create(
            substage.SubstageSnapshotId,
            "Find the key",
            "KEY-001",
            1,
            isActive: true,
            100,
            4.711,
            -74.0721,
            "Look near the entrance.",
            "VisibleAtStart");

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Multi-Device Mission",
            MaximumTime.Create(45),
            [
                StageSnapshot.Create("Stage One", 1, [substage])
            ],
            [target],
            []);
    }

    private static ParticipantTeamBoardDto CreateBoardDto(Guid liveSessionId, Guid teamId)
    {
        var timer = new SessionTimerSnapshotDto(
            liveSessionId,
            teamId,
            nameof(SessionState.Active),
            2700,
            2600,
            "Advancing",
            true,
            false,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            null);

        return new ParticipantTeamBoardDto(
            liveSessionId,
            "Test Mission",
            teamId,
            "Alpha",
            "A-01",
            0,
            timer,
            new ActiveSubstageContextDto(Guid.NewGuid(), "TreasureHunt", "Treasure Hunt", 1, 0, null, null, []),
            [],
            [],
            []);
    }

    private sealed record SeededSession(Guid LiveSessionId, Guid PrimaryTeamId, Guid SecondaryTeamId);

    // Races the first TeamBoardUpdated push against a timeout. Returns the payload if it arrives in
    // the window, or null otherwise — so both the positive (AC1/AC2) and negative (AC4) assertions
    // are strictly bounded and can never hang waiting for a message that is not coming.
    private sealed class BoundedBoardReceiver
    {
        private readonly Task<ParticipantTeamBoardDto> _received;

        public BoundedBoardReceiver(Task<ParticipantTeamBoardDto> received)
        {
            _received = received;
        }

        public async Task<ParticipantTeamBoardDto?> WaitAsync(TimeSpan timeout)
        {
            var completed = await Task.WhenAny(_received, Task.Delay(timeout));
            return completed == _received ? await _received : null;
        }
    }
}
