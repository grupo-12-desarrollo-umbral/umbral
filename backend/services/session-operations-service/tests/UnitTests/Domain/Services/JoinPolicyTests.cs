using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Services;

public sealed class JoinPolicyTests
{
    private readonly JoinPolicy _policy = new();

    [Fact]
    public void EnsureCanJoin_WhenTeamIsLocked_ThrowsException()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var team = session.RegisterTeam("Alpha", "A-01", 4);
        team.LockNewParticipants();

        var act = () => _policy.EnsureCanJoin(session, team);

        act.Should().Throw<TeamJoinClosedException>();
    }

    [Fact]
    public void EnsureCanJoin_WhenTeamIsAtCapacity_ThrowsException()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var team = session.RegisterTeam("Alpha", "A-01", 2);
        for (var i = 0; i < 2; i++)
        {
            session.AdmitParticipant(Guid.NewGuid(), $"P{i}", team.TeamId, DateTimeOffset.UtcNow.AddMinutes(i), _policy);
        }

        var act = () => _policy.EnsureCanJoin(session, team);

        act.Should().Throw<TeamCapacityReachedException>();
    }

    [Fact]
    public void EnsureCanReconnect_WhenParticipantTargetsDifferentTeam_ThrowsException()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var alpha = session.RegisterTeam("Alpha", "A-01", 4);
        var beta = session.RegisterTeam("Beta", "B-01", 4);
        var identityId = Guid.NewGuid();
        var joined = session.AdmitParticipant(identityId, "Nora", alpha.TeamId, DateTimeOffset.UtcNow, _policy);
        session.DisconnectParticipant(joined.Participant.SessionParticipantId, DateTimeOffset.UtcNow.AddMinutes(1));

        var act = () => _policy.EnsureCanReconnect(session, joined.Participant, alpha, beta.TeamId);

        act.Should().Throw<ParticipantAssignedToDifferentTeamException>();
    }

    [Fact]
    public void EnsureCanReconnect_WhenParticipantAlreadyActiveOnAssignedTeam_AllowsReconnect()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var alpha = session.RegisterTeam("Alpha", "A-01", 4);
        var identityId = Guid.NewGuid();
        var joined = session.AdmitParticipant(identityId, "Nora", alpha.TeamId, DateTimeOffset.UtcNow, _policy);

        var act = () => _policy.EnsureCanReconnect(session, joined.Participant, alpha, alpha.TeamId);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureCanReconnect_WhenParticipantRemoved_ThrowsException()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var alpha = session.RegisterTeam("Alpha", "A-01", 4);
        var joined = session.AdmitParticipant(Guid.NewGuid(), "Nora", alpha.TeamId, DateTimeOffset.UtcNow, _policy);
        joined.Participant.Remove(DateTimeOffset.UtcNow.AddMinutes(1));

        var act = () => _policy.EnsureCanReconnect(session, joined.Participant, alpha, alpha.TeamId);

        act.Should().Throw<ParticipantRemovedFromSessionException>();
    }
}
