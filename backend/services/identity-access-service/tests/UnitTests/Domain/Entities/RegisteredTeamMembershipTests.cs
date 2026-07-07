using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Domain.Entities;

public sealed class RegisteredTeamMembershipTests
{
    [Fact]
    public void AuthorizeParticipant_AddsWhitelistEntry_WithoutAssignmentSemantics()
    {
        var team = RegisteredTeam.Register("Red Foxes", "RF-01");
        const int userId = 42;

        var membership = team.AuthorizeParticipant(userId);

        membership.Should().BeAssignableTo<RegisteredTeamMembership>();
        membership.TeamMembershipId.Should().NotBe(Guid.Empty);
        membership.TeamId.Should().Be(team.TeamId);
        membership.UserId.Should().Be(userId);
        team.Memberships.Should().ContainSingle().Which.Should().Be(membership);
        // Authorization is may-join eligibility, not an assignment: it raises no event
        // beyond the team's own registration event.
        team.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<TeamRegisteredEvent>();
    }

    [Fact]
    public void AuthorizeParticipant_WhenAlreadyAuthorized_Throws()
    {
        var team = RegisteredTeam.Register("Red Foxes", "RF-01");
        team.AuthorizeParticipant(42);

        FluentActions.Invoking(() => team.AuthorizeParticipant(42))
            .Should().Throw<ParticipantAlreadyAuthorizedForTeamException>();
    }

    [Fact]
    public void AuthorizeParticipant_OnInactiveTeam_Throws()
    {
        var team = RegisteredTeam.Register("Red Foxes", "RF-01");
        team.Deactivate();

        FluentActions.Invoking(() => team.AuthorizeParticipant(42))
            .Should().Throw<TeamNotActiveException>();
    }
}
