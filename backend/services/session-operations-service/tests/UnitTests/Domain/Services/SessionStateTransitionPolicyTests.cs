using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Services;

public sealed class SessionStateTransitionPolicyTests
{
    private readonly SessionStateTransitionPolicy _policy = new();

    // Canonical transition matrix (CONTEXT.md §SessionState):
    //   Scheduled → {Preparing, Cancelled}
    //   Preparing → {Active, Cancelled}
    //   Active    → {Paused, Finished, Cancelled}
    //   Paused    → {Active, Finished, Cancelled}
    //   Finished / Cancelled: terminal (no outgoing edges)
    private static readonly (SessionState From, SessionState To)[] CanonicalEdges =
    {
        (SessionState.Scheduled, SessionState.Preparing),
        (SessionState.Scheduled, SessionState.Cancelled),
        (SessionState.Preparing, SessionState.Active),
        (SessionState.Preparing, SessionState.Cancelled),
        (SessionState.Active, SessionState.Paused),
        (SessionState.Active, SessionState.Finished),
        (SessionState.Active, SessionState.Cancelled),
        (SessionState.Paused, SessionState.Active),
        (SessionState.Paused, SessionState.Finished),
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
