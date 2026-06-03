using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Services;

public sealed class SessionStateTransitionPolicyTests
{
    private readonly SessionStateTransitionPolicy _policy = new();

    [Fact]
    public void EnsureCanTransition_ToActiveWithoutTeams_ThrowsException()
    {
        var act = () => _policy.EnsureCanTransition(SessionState.Preparing, SessionState.Active, 0);

        act.Should().Throw<LiveSessionRequiresAtLeastOneTeamException>();
    }

    [Fact]
    public void EnsureCanTransition_FromFinishedToActive_ThrowsException()
    {
        var act = () => _policy.EnsureCanTransition(SessionState.Finished, SessionState.Active, 1);

        act.Should().Throw<InvalidSessionStateTransitionException>();
    }
}
