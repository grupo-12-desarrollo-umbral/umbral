using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class LiveSessionRepositoryIntegrationTests
{
    private readonly PersistenceTestContextFactory _contextFactory;

    public LiveSessionRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task GetByIdAsync_RestoresRuntimeParticipationState()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        var liveSession = CreateSession(createdAt);
        var team = liveSession.RegisterTeam("Blue", "BLUE-01");
        var participant = liveSession.AdmitParticipant(
            Guid.NewGuid(),
            "Nora",
            team.TeamId,
            createdAt.AddMinutes(2),
            4,
            new JoinPolicy()).Participant;
        liveSession.OpenJoinContext(team.TeamId, Guid.NewGuid(), createdAt.AddMinutes(1), createdAt.AddMinutes(11));
        liveSession.DisconnectParticipant(participant.SessionParticipantId, createdAt.AddMinutes(5));

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var repository = new LiveSessionRepository(assertContext);
        var persistedSession = await repository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        persistedSession.Should().NotBeNull();
        persistedSession!.Teams.Should().ContainSingle();
        persistedSession.Participants.Should().ContainSingle();
        persistedSession.JoinContexts.Should().ContainSingle();
        persistedSession.Created.Should().BeAfter(DateTimeOffset.MinValue);
        persistedSession.LastModified.Should().BeAfter(DateTimeOffset.MinValue);

        var persistedTeam = persistedSession.Teams.Single();
        persistedTeam.TeamCode.Value.Should().Be("BLUE-01");
        persistedTeam.Members.Should().ContainSingle(member => member.SessionParticipantId == participant.SessionParticipantId);

        var persistedParticipant = persistedSession.Participants.Single();
        persistedParticipant.SessionParticipantId.Should().Be(participant.SessionParticipantId);
        persistedParticipant.IsDisconnected.Should().BeTrue();
        persistedParticipant.LastSeenAt.Should().BeCloseTo(createdAt.AddMinutes(5), TimeSpan.FromMicroseconds(1));
    }

    [Fact]
    public async Task UpdateAsync_PersistsReconnectRecoveryWithoutDuplicatingMembership()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var joinedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var liveSession = CreateSession(joinedAt.AddMinutes(-5));
        var team = liveSession.RegisterTeam("Red", "RED-01");
        var participant = liveSession.AdmitParticipant(
            Guid.NewGuid(),
            "Nova",
            team.TeamId,
            joinedAt,
            3,
            new JoinPolicy()).Participant;
        liveSession.DisconnectParticipant(participant.SessionParticipantId, joinedAt.AddMinutes(2));

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        var reconnectedAt = joinedAt.AddMinutes(7);

        await using (var actContext = BuildContext())
        {
            var repository = new LiveSessionRepository(actContext);
            var persistedSession = await repository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

            persistedSession.Should().NotBeNull();

            var admission = persistedSession!.AdmitParticipant(
                participant.ExternalIdentityId,
                participant.DisplayName,
                team.TeamId,
                reconnectedAt,
                3,
                new JoinPolicy());

            admission.IsReconnect.Should().BeTrue();
            admission.Participant.SessionParticipantId.Should().Be(participant.SessionParticipantId);

            await repository.UpdateAsync(persistedSession, CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var reloadedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        reloadedSession.Should().NotBeNull();

        var reloadedParticipant = reloadedSession!.Participants.Single();
        reloadedParticipant.IsDisconnected.Should().BeFalse();
        reloadedParticipant.LastSeenAt.Should().BeCloseTo(reconnectedAt, TimeSpan.FromMicroseconds(1));

        var reloadedTeam = reloadedSession.Teams.Single();
        reloadedTeam.Members.Should().ContainSingle(member => member.SessionParticipantId == participant.SessionParticipantId);
    }

    [Fact]
    public async Task UpdateAsync_PersistsAssociatedTeamsAndQueriesThemBackBySession()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(-15);
        var liveSession = CreateSession(scheduledAt);
        var referenceTeamId = Guid.NewGuid();
        var associatedTeam = liveSession.AssociateTeam(referenceTeamId, "Aurora", "AUR-01");

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var persistedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        persistedSession.Should().NotBeNull();
        persistedSession!.Teams.Should().ContainSingle(team => team.ReferenceTeamId == referenceTeamId);

        var persistedTeam = persistedSession.Teams.Single();
        persistedTeam.TeamId.Should().Be(associatedTeam.TeamId);
        persistedTeam.DisplayName.Should().Be("Aurora");
        persistedTeam.TeamCode.Value.Should().Be("AUR-01");
        persistedTeam.ReferenceTeamId.Should().Be(referenceTeamId);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateAssociationAttemptLeavesPersistedSessionUnchanged()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var liveSession = CreateSession(scheduledAt);
        var referenceTeamId = Guid.NewGuid();
        liveSession.AssociateTeam(referenceTeamId, "Vector", "VEC-01");

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (var actContext = BuildContext())
        {
            var repository = new LiveSessionRepository(actContext);
            var persistedSession = await repository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

            persistedSession.Should().NotBeNull();

            var duplicateAttempt = () => persistedSession!.AssociateTeam(referenceTeamId, "Vector", "VEC-01");
            duplicateAttempt.Should().Throw<DuplicateTeamAssociationInSessionException>();

            await repository.UpdateAsync(persistedSession!, CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var reloadedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        reloadedSession.Should().NotBeNull();
        reloadedSession!.Teams.Should().ContainSingle();

        var persistedTeam = reloadedSession.Teams.Single();
        persistedTeam.ReferenceTeamId.Should().Be(referenceTeamId);
        persistedTeam.TeamCode.Value.Should().Be("VEC-01");
    }

    private ApplicationDbContext BuildContext()
    {
        return _contextFactory.Create();
    }

    private static async Task ResetDatabaseAsync(ApplicationDbContext context)
    {
        // Deleting the aggregate root cascades to teams, participants, join contexts,
        // and team members (all FKs are ON DELETE CASCADE).
        await context.LiveSessions.ExecuteDeleteAsync();
    }

    private static LiveSession CreateSession(DateTimeOffset scheduledAt)
    {
        return LiveSession.Create(
            SessionMode.TreasureHunt,
            SessionSource.Create(SessionSourceType.Mission, Guid.NewGuid()),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Reconnect Session",
            45,
            scheduledAt);
    }
}
