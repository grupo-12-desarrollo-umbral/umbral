using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

// Concurrency Finding 5 (reconnect/disconnect overlap), persistence lock. Presence is a
// decrement-guarded set of per-connection leases keyed on ConnectionId, persisted as
// live_session_participant_connections. Two things must hold end-to-end against Postgres:
//
//   1. The overlap is serialized. An old socket's disconnect that loses the xmin race to a fresh
//      socket's reconnect surfaces as ConcurrentModificationException (the retry then re-evaluates
//      against the committed new lease and leaves the participant Active) — it never blindly marks a
//      genuinely-connected participant Disconnected.
//   2. Leases round-trip. A reload rebuilds the connection set so the decrement guard sees prior
//      sockets, and only the last lease dropping flips the participant to Disconnected.
[Collection(PostgreSqlCollection.Name)]
public sealed class LiveSessionConnectionPresenceRepositoryIntegrationTests
{
    private readonly PersistenceTestContextFactory _contextFactory;

    public LiveSessionConnectionPresenceRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task OldSocketDisconnect_LosingXminRaceToFreshReconnect_ConflictsThenLeavesParticipantActive()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var joinedAt = DateTimeOffset.UtcNow.AddMinutes(-15);
        var liveSession = CreateSession(joinedAt.AddMinutes(-5));
        var team = liveSession.RegisterTeam("Blue", "BLUE-01", 4);
        var externalIdentityId = Guid.NewGuid();
        var participantId = liveSession
            .AdmitParticipant(externalIdentityId, "Nora", team.TeamId, joinedAt, new JoinPolicy())
            .Participant.SessionParticipantId;
        // Seed the participant already holding one live socket.
        liveSession.RegisterParticipantConnection(participantId, "old-socket", joinedAt);

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        // Two independently-loaded aggregates from the SAME xmin: one about to disconnect the old
        // socket, one about to admit a fresh socket. Load both before either writes.
        await using var disconnectContext = BuildContext();
        await using var reconnectContext = BuildContext();
        var disconnectRepository = new LiveSessionRepository(disconnectContext);
        var reconnectRepository = new LiveSessionRepository(reconnectContext);

        var disconnectView = await disconnectRepository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);
        var reconnectView = await reconnectRepository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);
        disconnectView.Should().NotBeNull();
        reconnectView.Should().NotBeNull();

        // Fresh socket reconnects and commits first, bumping xmin.
        reconnectView!.RegisterParticipantConnection(participantId, "new-socket", joinedAt.AddSeconds(3));
        await reconnectRepository.UpdateAsync(reconnectView, CancellationToken.None);

        // Old socket's delayed disconnect now writes against the stale xmin and loses the race.
        disconnectView!.DisconnectParticipantConnection(participantId, "old-socket", joinedAt.AddSeconds(4));
        var losingSave = async () => await disconnectRepository.UpdateAsync(disconnectView, CancellationToken.None);
        await losingSave.Should().ThrowAsync<ConcurrentModificationException>(
            "the disconnect lost the xmin race and must retry rather than overwrite the fresh reconnect");

        // The retry (what ConcurrencyRetryBehaviour drives) reloads fresh state and drops the old lease
        // again — now against the committed set that already contains the new socket.
        await using (var retryContext = BuildContext())
        {
            var retryRepository = new LiveSessionRepository(retryContext);
            var retryView = await retryRepository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);
            retryView!.DisconnectParticipantConnection(participantId, "old-socket", joinedAt.AddSeconds(5));
            await retryRepository.UpdateAsync(retryView, CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var reloaded = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);
        var participant = reloaded!.Participants.Single();
        // The genuinely-connected participant stays Active: only the old lease was removed.
        participant.ParticipantStatus.Should().Be(ParticipantStatus.Active);
        participant.Connections.Select(connection => connection.ConnectionId).Should().BeEquivalentTo(["new-socket"]);
    }

    [Fact]
    public async Task Presence_FlipsToDisconnected_OnlyWhenTheLastLeaseDropsAcrossReloads()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var joinedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var liveSession = CreateSession(joinedAt.AddMinutes(-5));
        var team = liveSession.RegisterTeam("Blue", "BLUE-01", 4);
        var participantId = liveSession
            .AdmitParticipant(Guid.NewGuid(), "Nora", team.TeamId, joinedAt, new JoinPolicy())
            .Participant.SessionParticipantId;
        liveSession.RegisterParticipantConnection(participantId, "socket-1", joinedAt);
        liveSession.RegisterParticipantConnection(participantId, "socket-2", joinedAt.AddSeconds(1));

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        // Drop the first socket through a reloaded aggregate — the second lease keeps the participant up.
        await using (var firstDropContext = BuildContext())
        {
            var repository = new LiveSessionRepository(firstDropContext);
            var loaded = await repository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);
            loaded!.DisconnectParticipantConnection(participantId, "socket-1", joinedAt.AddSeconds(30));
            await repository.UpdateAsync(loaded, CancellationToken.None);
        }

        await using (var afterFirstContext = BuildContext())
        {
            var reloaded = await new LiveSessionRepository(afterFirstContext)
                .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);
            var participant = reloaded!.Participants.Single();
            participant.ParticipantStatus.Should().Be(ParticipantStatus.Active);
            participant.Connections.Select(connection => connection.ConnectionId).Should().BeEquivalentTo(["socket-2"]);
        }

        // Drop the last socket — now presence flips to Disconnected.
        var lastDroppedAt = joinedAt.AddSeconds(45);
        await using (var lastDropContext = BuildContext())
        {
            var repository = new LiveSessionRepository(lastDropContext);
            var loaded = await repository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);
            loaded!.DisconnectParticipantConnection(participantId, "socket-2", lastDroppedAt);
            await repository.UpdateAsync(loaded, CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var finalSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);
        var finalParticipant = finalSession!.Participants.Single();
        finalParticipant.ParticipantStatus.Should().Be(ParticipantStatus.Disconnected);
        finalParticipant.LastSeenAt.Should().BeCloseTo(lastDroppedAt, TimeSpan.FromMicroseconds(1));
        finalParticipant.ActiveConnectionCount.Should().Be(0);
    }

    private ApplicationDbContext BuildContext() => _contextFactory.Create();

    private static async Task ResetDatabaseAsync(ApplicationDbContext context)
    {
        await context.LiveSessions.ExecuteDeleteAsync();
    }

    private static LiveSession CreateSession(DateTimeOffset scheduledAt)
    {
        var sourceMissionId = Guid.NewGuid();

        return LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Connection Presence Session",
            45,
            scheduledAt,
            CreateTreasureHuntRuntimeSnapshot(sourceMissionId, 45));
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntRuntimeSnapshot(Guid sourceMissionId, int maximumTimeMinutes)
    {
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Mission Runtime",
            MaximumTime.Create(maximumTimeMinutes),
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
}
