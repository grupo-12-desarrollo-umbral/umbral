using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Domain.Services;

public sealed class ParticipantSessionMembershipPolicyTests
{
    private readonly ParticipantSessionMembershipPolicy _policy = new();

    [Fact]
    public void EnsureCanSelfAssign_AllowsParticipantWithoutExistingSessionMembership()
    {
        var act = () => _policy.EnsureCanSelfAssign(Guid.NewGuid(), 42, existingTeamId: null);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureCanSelfAssign_AllowsParticipantReEnteringOwnTeam()
    {
        var teamId = Guid.NewGuid();

        var act = () => _policy.EnsureCanSelfAssign(teamId, 42, teamId);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureCanSelfAssign_RejectsParticipantLockedToAnotherTeam()
    {
        var currentTeamId = Guid.NewGuid();
        var requestedTeamId = Guid.NewGuid();

        FluentActions.Invoking(() => _policy.EnsureCanSelfAssign(requestedTeamId, 42, currentTeamId))
            .Should().Throw<ParticipantLockedToAnotherSessionTeamException>();
    }
}
