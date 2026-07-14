using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

// HU-08 (AC3) verification + regression lock. Multi-device sync adds NO new column: presence
// recovery state (ParticipantStatus, LastSeenAt) and team membership are already persisted by
// migration 20260603145000_AddRuntimeRecoveryState under LiveSessionConfiguration. This drives a
// full disconnect -> reconnect cycle through the REAL LiveSessionRepository against Testcontainers
// Postgres, reloading from the DB at each stage, and proves the reloaded aggregate preserves the
// participant's ParticipantStatus, LastSeenAt heartbeat, and team assignment across the cycle.
[Collection(PostgreSqlCollection.Name)]
public sealed class LiveSessionMultiDeviceReconnectRepositoryIntegrationTests
{
    private readonly PersistenceTestContextFactory _contextFactory;

    public LiveSessionMultiDeviceReconnectRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task DisconnectThenReconnect_PreservesParticipantStatusLastSeenAndTeamAssignmentAcrossReload()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var joinedAt = DateTimeOffset.UtcNow.AddMinutes(-15);
        var liveSession = CreateSession(joinedAt.AddMinutes(-5));
        var team = liveSession.RegisterTeam("Blue", "BLUE-01", 4);
        var externalIdentityId = Guid.NewGuid();

        var admission = liveSession.AdmitParticipant(
            externalIdentityId,
            "Nora",
            team.TeamId,
            joinedAt,
            new JoinPolicy());
        var participantId = admission.Participant.SessionParticipantId;

        // Sanity: a fresh first join lands the participant Active on the requested team.
        admission.IsReconnect.Should().BeFalse();
        admission.Participant.ParticipantStatus.Should().Be(ParticipantStatus.Active);

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        // --- Disconnect: reload from DB, drop presence, save back through the repository. ---
        var disconnectedAt = joinedAt.AddMinutes(3);

        await using (var disconnectContext = BuildContext())
        {
            var repository = new LiveSessionRepository(disconnectContext);
            var loaded = await repository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

            loaded.Should().NotBeNull();
            loaded!.DisconnectParticipant(participantId, disconnectedAt);

            await repository.UpdateAsync(loaded, CancellationToken.None);
        }

        await using (var afterDisconnectContext = BuildContext())
        {
            var reloaded = await new LiveSessionRepository(afterDisconnectContext)
                .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

            reloaded.Should().NotBeNull();

            var participant = reloaded!.Participants.Single();
            participant.SessionParticipantId.Should().Be(participantId);
            // Presence status round-trips as the Disconnected enum value, not a lossy bool.
            participant.ParticipantStatus.Should().Be(ParticipantStatus.Disconnected);
            participant.LastSeenAt.Should().BeCloseTo(disconnectedAt, TimeSpan.FromMicroseconds(1));

            // Team assignment survives the disconnect: the slot is kept while offline.
            var assignedTeam = reloaded.FindTeamForExternalParticipant(externalIdentityId);
            assignedTeam.Should().NotBeNull();
            assignedTeam!.TeamId.Should().Be(team.TeamId);
            assignedTeam.Members.Should().ContainSingle(member => member.SessionParticipantId == participantId);
        }

        // --- Reconnect: returning-identity branch is idempotent (RefreshPresence, IsReconnect=true). ---
        var reconnectedAt = joinedAt.AddMinutes(8);

        await using (var reconnectContext = BuildContext())
        {
            var repository = new LiveSessionRepository(reconnectContext);
            var loaded = await repository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

            loaded.Should().NotBeNull();

            var reconnect = loaded!.AdmitParticipant(
                externalIdentityId,
                "Nora",
                team.TeamId,
                reconnectedAt,
                new JoinPolicy());

            reconnect.IsReconnect.Should().BeTrue();
            reconnect.Participant.SessionParticipantId.Should().Be(participantId);

            await repository.UpdateAsync(loaded, CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var reconnectedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        reconnectedSession.Should().NotBeNull();

        // Exactly one participant — reconnect must not create a duplicate identity.
        var reconnectedParticipant = reconnectedSession!.Participants.Single();
        reconnectedParticipant.SessionParticipantId.Should().Be(participantId);
        // Presence recovered to Active with the reconnect heartbeat.
        reconnectedParticipant.ParticipantStatus.Should().Be(ParticipantStatus.Active);
        reconnectedParticipant.LastSeenAt.Should().BeCloseTo(reconnectedAt, TimeSpan.FromMicroseconds(1));

        // Team assignment preserved end-to-end — no duplicated membership on the same team.
        var reconnectedTeam = reconnectedSession.FindTeamForExternalParticipant(externalIdentityId);
        reconnectedTeam.Should().NotBeNull();
        reconnectedTeam!.TeamId.Should().Be(team.TeamId);
        reconnectedTeam.Members.Should().ContainSingle(member => member.SessionParticipantId == participantId);
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
            "Multi-Device Reconnect Session",
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
