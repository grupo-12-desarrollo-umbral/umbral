using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Services;

public sealed class SessionStateTransitionPolicyTests
{
    private readonly SessionStateTransitionPolicy _policy = new();

    // Canonical transition matrix (CONTEXT.md §SessionState) for the *manual/generic* transition
    // path (SessionStateTransitionPolicy / CurrentStateGate). Finished is deliberately absent from
    // Active's and Paused's outgoing edges here: per canon, Finished is reached ONLY through
    // SessionCompletion (LiveSession.CompleteActiveSubstageAndAdvance applies it directly, bypassing
    // this policy), never via manual Operator transition.
    //   Scheduled → {Preparing, Cancelled}
    //   Preparing → {Active, Cancelled}
    //   Active    → {Paused, Cancelled}
    //   Paused    → {Active, Cancelled}
    //   Finished / Cancelled: terminal (no outgoing edges)
    private static readonly (SessionState From, SessionState To)[] CanonicalEdges =
    {
        (SessionState.Scheduled, SessionState.Preparing),
        (SessionState.Scheduled, SessionState.Cancelled),
        (SessionState.Preparing, SessionState.Active),
        (SessionState.Preparing, SessionState.Cancelled),
        (SessionState.Active, SessionState.Paused),
        (SessionState.Active, SessionState.Cancelled),
        (SessionState.Paused, SessionState.Active),
        (SessionState.Paused, SessionState.Cancelled),
    };

    public static TheoryData<SessionState, SessionState> AllowedEdges => Build(includeAllowed: true);

    public static TheoryData<SessionState, SessionState> RejectedEdges => Build(includeAllowed: false);

    // One assertion per canonical edge — every allowed transition is reachable.
    [Theory]
    [MemberData(nameof(AllowedEdges))]
    public void IsTransitionAllowed_ForCanonicalEdge_ReturnsTrue(SessionState current, SessionState next)
    {
        _policy.IsTransitionAllowed(current, next).Should().BeTrue();
    }

    // One assertion per rejection — every non-canonical edge (incl. self-loops
    // and all outgoing edges from terminal states) is refused.
    [Theory]
    [MemberData(nameof(RejectedEdges))]
    public void IsTransitionAllowed_ForNonCanonicalEdge_ReturnsFalse(SessionState current, SessionState next)
    {
        _policy.IsTransitionAllowed(current, next).Should().BeFalse();
    }

    [Theory]
    [InlineData(SessionState.Finished)]
    [InlineData(SessionState.Cancelled)]
    public void TerminalState_HasNoOutgoingTransition(SessionState terminal)
    {
        foreach (SessionState next in Enum.GetValues<SessionState>())
        {
            _policy.IsTransitionAllowed(terminal, next).Should().BeFalse();
        }
    }

    [Theory]
    [InlineData(SessionState.Scheduled)]
    [InlineData(SessionState.Preparing)]
    [InlineData(SessionState.Active)]
    [InlineData(SessionState.Paused)]
    public void Cancelled_IsReachableFromEveryNonTerminalState(SessionState nonTerminal)
    {
        _policy.IsTransitionAllowed(nonTerminal, SessionState.Cancelled).Should().BeTrue();
    }

    [Fact]
    public void EnsureCanTransition_ToActiveWithoutTeams_ThrowsException()
    {
        var act = () => _policy.EnsureCanTransition(SessionState.Preparing, SessionState.Active, 0);

        act.Should().Throw<LiveSessionRequiresAtLeastOneTeamException>();
    }

    [Fact]
    public void EnsureCanTransition_OverRejectedEdge_ThrowsInvalidTransition()
    {
        var act = () => _policy.EnsureCanTransition(SessionState.Finished, SessionState.Active, 1);

        act.Should().Throw<InvalidSessionStateTransitionException>();
    }

    // Manual Active/Paused -> Finished must be rejected: Finished is reached ONLY via
    // SessionCompletion, never via the generic Operator transition path.
    [Theory]
    [InlineData(SessionState.Active)]
    [InlineData(SessionState.Paused)]
    public void EnsureCanTransition_ManualToFinished_ThrowsInvalidTransition(SessionState current)
    {
        var act = () => _policy.EnsureCanTransition(current, SessionState.Finished, associatedTeamCount: 1);

        act.Should().Throw<InvalidSessionStateTransitionException>();
    }

    // Issue-2 fix: structural reachability (CanTransitionTo) must be checked before the no-team
    // rule, matching CurrentStateGate's order, so the same invalid request reports the same reason
    // regardless of entry path. Scheduled -> Active is both structurally unreachable AND missing
    // teams; the structural error must win.
    [Fact]
    public void EnsureCanTransition_StructurallyUnreachableAndNoTeams_ThrowsInvalidTransitionNotTeamRequirement()
    {
        var act = () => _policy.EnsureCanTransition(SessionState.Scheduled, SessionState.Active, associatedTeamCount: 0);

        act.Should().Throw<InvalidSessionStateTransitionException>();
    }

    private static TheoryData<SessionState, SessionState> Build(bool includeAllowed)
    {
        var canonical = new HashSet<(SessionState, SessionState)>(CanonicalEdges);
        var data = new TheoryData<SessionState, SessionState>();

        foreach (SessionState current in Enum.GetValues<SessionState>())
        {
            foreach (SessionState next in Enum.GetValues<SessionState>())
            {
                if (canonical.Contains((current, next)) == includeAllowed)
                {
                    data.Add(current, next);
                }
            }
        }

        return data;
    }
}
