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

    [Theory]
    [InlineData(SessionState.Scheduled, SessionState.Preparing)]
    [InlineData(SessionState.Scheduled, SessionState.Cancelled)]
    [InlineData(SessionState.Preparing, SessionState.Active)]
    [InlineData(SessionState.Active, SessionState.Paused)]
    [InlineData(SessionState.Active, SessionState.Finished)]
    [InlineData(SessionState.Paused, SessionState.Active)]
    public void IsTransitionAllowed_ForReachableEdge_ReturnsTrue(SessionState current, SessionState next)
    {
        _policy.IsTransitionAllowed(current, next).Should().BeTrue();
    }

    [Theory]
    [InlineData(SessionState.Scheduled, SessionState.Active)]
    [InlineData(SessionState.Scheduled, SessionState.Finished)]
    [InlineData(SessionState.Finished, SessionState.Active)]
    [InlineData(SessionState.Cancelled, SessionState.Preparing)]
    [InlineData(SessionState.Active, SessionState.Scheduled)]
    public void IsTransitionAllowed_ForUnreachableEdge_ReturnsFalse(SessionState current, SessionState next)
    {
        _policy.IsTransitionAllowed(current, next).Should().BeFalse();
    }
}
