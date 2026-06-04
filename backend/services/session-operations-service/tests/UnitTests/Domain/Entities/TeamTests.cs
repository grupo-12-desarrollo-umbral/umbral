using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

public sealed class TeamTests
{
    [Fact]
    public void Register_WithMissingDisplayName_ThrowsException()
    {
        var act = () => Team.Register(Guid.NewGuid(), string.Empty, "A-01", 4);

        act.Should().Throw<TeamDisplayNameRequiredException>();
    }

    [Fact]
    public void Register_WithExplicitTeamId_PreservesTeamId()
    {
        var teamId = Guid.NewGuid();

        var team = Team.Register(Guid.NewGuid(), teamId, "Alpha", "A-01", 4);

        team.TeamId.Should().Be(teamId);
        team.ReferenceTeamId.Should().BeNull();
    }

    [Fact]
    public void Register_WithEmptyTeamId_ThrowsException()
    {
        var act = () => Team.Register(Guid.NewGuid(), Guid.Empty, "Alpha", "A-01", 4);

        act.Should().Throw<TeamIdentityRequiredException>();
    }

    [Fact]
    public void Register_WithNonPositiveCapacity_ThrowsException()
    {
        var act = () => Team.Register(Guid.NewGuid(), "Alpha", "A-01", 0);

        act.Should().Throw<TeamCapacityMustBePositiveException>();
    }

    [Fact]
    public void Associate_WithReferenceTeamId_PreservesReferenceCorrelation()
    {
        var referenceTeamId = Guid.NewGuid();

        var team = Team.Associate(Guid.NewGuid(), referenceTeamId, "Alpha", "A-01", 2);

        team.TeamId.Should().NotBe(referenceTeamId);
        team.ReferenceTeamId.Should().Be(referenceTeamId);
        team.Capacity.Should().Be(2);
    }

    [Fact]
    public void Associate_WithEmptyReferenceTeamId_ThrowsException()
    {
        var act = () => Team.Associate(Guid.NewGuid(), Guid.Empty, "Alpha", "A-01", 2);

        act.Should().Throw<ReferenceTeamIdRequiredException>();
    }

    [Fact]
    public void CloseNewParticipants_ChangesJoinStatus()
    {
        var team = Team.Register(Guid.NewGuid(), "Alpha", "A-01", 4);

        team.CloseNewParticipants();

        team.JoinStatus.Should().Be(TeamJoinStatus.Closed);
    }

    [Fact]
    public void LockAndReopenNewParticipants_ChangesJoinStatus()
    {
        var team = Team.Register(Guid.NewGuid(), "Alpha", "A-01", 4);

        team.LockNewParticipants();
        team.ReopenForNewParticipants();

        team.JoinStatus.Should().Be(TeamJoinStatus.Open);
    }
}
