using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

public sealed class TeamTests
{
    [Fact]
    public void Register_WithMissingDisplayName_ThrowsException()
    {
        var act = () => Team.Register(Guid.NewGuid(), string.Empty, "A-01");

        act.Should().Throw<TeamDisplayNameRequiredException>();
    }

    [Fact]
    public void Associate_WithReferenceTeamId_TracksIdentityCorrelation()
    {
        var referenceTeamId = Guid.NewGuid();

        var team = Team.Associate(Guid.NewGuid(), referenceTeamId, "Alpha", "A-01");

        team.ReferenceTeamId.Should().Be(referenceTeamId);
    }

    [Fact]
    public void Associate_WithEmptyReferenceTeamId_ThrowsException()
    {
        var act = () => Team.Associate(Guid.NewGuid(), Guid.Empty, "Alpha", "A-01");

        act.Should().Throw<ReferenceTeamIdRequiredException>();
    }

    [Fact]
    public void CloseNewParticipants_ChangesJoinStatus()
    {
        var team = Team.Register(Guid.NewGuid(), "Alpha", "A-01");

        team.CloseNewParticipants();

        team.JoinStatus.Should().Be(TeamJoinStatus.Closed);
    }

    [Fact]
    public void LockAndReopenNewParticipants_ChangesJoinStatus()
    {
        var team = Team.Register(Guid.NewGuid(), "Alpha", "A-01");

        team.LockNewParticipants();
        team.ReopenForNewParticipants();

        team.JoinStatus.Should().Be(TeamJoinStatus.Open);
    }
}
