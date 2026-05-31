using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.UnitTests.Domain.Entities;

public sealed class TeamMembershipTests
{
    [Fact]
    public void AssignParticipant_CreatesMembershipShapeExposedThroughTeam()
    {
        var team = Team.Register("Red Foxes", "RF-01");
        const int userId = 42;

        var membership = team.AssignParticipant(userId);

        membership.Should().BeAssignableTo<TeamMembership>();
        membership.TeamMembershipId.Should().NotBe(Guid.Empty);
        membership.TeamId.Should().Be(team.TeamId);
        membership.UserId.Should().Be(userId);
        membership.AssignedAt.Should().NotBe(default);
    }
}
