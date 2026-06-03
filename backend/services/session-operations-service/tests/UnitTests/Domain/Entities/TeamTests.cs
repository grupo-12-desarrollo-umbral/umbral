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
