using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

// Pre-start team pick/switch (#89): switching within the authorized set, capacity-checked, with the
// old slot freed; frozen once the session starts.
public sealed class LiveSessionSelectTeamTests
{
    private static readonly Guid RedRef = Guid.NewGuid();
    private static readonly Guid BlueRef = Guid.NewGuid();
    private static readonly DateTimeOffset At = new(2026, 6, 3, 9, 0, 0, TimeSpan.Zero);

    private readonly OpenTeamSelectionPolicy _policy = new();
    private readonly SessionStateTransitionPolicy _transitions = new();

    [Fact]
    public void SelectTeam_FirstPick_CreatesParticipantAndMember()
    {
        var session = CreateSession(redCapacity: 4, out var red, out _);

        var (participant, team) = session.SelectTeam(Guid.NewGuid(), "Nora", red.TeamId, Empty, At, _policy);

        team.TeamId.Should().Be(red.TeamId);
        red.ActiveMemberCount.Should().Be(1);
        session.Participants.Should().ContainSingle().Which.Should().Be(participant);
    }

    [Fact]
    public void SelectTeam_Switch_FreesOldSlotAndOccupiesNew()
    {
        var session = CreateSession(redCapacity: 4, out var red, out var blue);
        var identity = Guid.NewGuid();
        session.SelectTeam(identity, "Nora", red.TeamId, Empty, At, _policy);

        session.SelectTeam(identity, "Nora", blue.TeamId, Empty, At, _policy);

        red.ActiveMemberCount.Should().Be(0);
        blue.ActiveMemberCount.Should().Be(1);
        session.Participants.Should().ContainSingle(); // switch, not a second participant
    }

    [Fact]
    public void SelectTeam_WhenTargetFull_ThrowsCapacity()
    {
        var session = CreateSession(redCapacity: 1, out var red, out _);
        session.SelectTeam(Guid.NewGuid(), "First", red.TeamId, Empty, At, _policy);

        var act = () => session.SelectTeam(Guid.NewGuid(), "Second", red.TeamId, Empty, At, _policy);

        act.Should().Throw<TeamCapacityReachedException>();
    }

    [Fact]
    public void SelectTeam_WhenSwitchingOut_FreesSlotForAnother()
    {
        var session = CreateSession(redCapacity: 1, out var red, out var blue);
        var first = Guid.NewGuid();
        session.SelectTeam(first, "First", red.TeamId, Empty, At, _policy);

        session.SelectTeam(first, "First", blue.TeamId, Empty, At, _policy); // frees Red's only slot
        var act = () => session.SelectTeam(Guid.NewGuid(), "Second", red.TeamId, Empty, At, _policy);

        act.Should().NotThrow();
        red.ActiveMemberCount.Should().Be(1);
    }

    [Fact]
    public void SelectTeam_RePickingCurrentTeam_IsNoOp()
    {
        var session = CreateSession(redCapacity: 1, out var red, out _);
        var identity = Guid.NewGuid();
        session.SelectTeam(identity, "Nora", red.TeamId, Empty, At, _policy);

        var act = () => session.SelectTeam(identity, "Nora", red.TeamId, Empty, At, _policy);

        act.Should().NotThrow(); // idempotent — does not trip its own capacity of 1
        red.ActiveMemberCount.Should().Be(1);
    }

    [Fact]
    public void SelectTeam_OutsideAuthorizedSet_Throws()
    {
        var session = CreateSession(redCapacity: 4, out _, out var blue);

        var act = () => session.SelectTeam(Guid.NewGuid(), "Nora", blue.TeamId, SetOf(RedRef), At, _policy);

        act.Should().Throw<TeamNotInAuthorizedSetException>();
    }

    [Fact]
    public void SelectTeam_OnceSessionActive_ThrowsFrozen()
    {
        var session = CreateSession(redCapacity: 4, out var red, out _);
        session.MoveTo(SessionState.Preparing, At, _transitions);
        session.MoveTo(SessionState.Active, At, _transitions);

        var act = () => session.SelectTeam(Guid.NewGuid(), "Nora", red.TeamId, Empty, At, _policy);

        act.Should().Throw<OpenTeamSelectionClosedException>();
    }

    [Fact]
    public void MoveToActive_DoesNotAutoAssignTeamlessParticipants()
    {
        // Decisions §8: a participant with no team by Active is not admitted; there is NO
        // auto-seating. Guards against a future "helpful" auto-assign in the Active transition.
        var session = CreateSession(redCapacity: 4, out _, out _);
        session.MoveTo(SessionState.Preparing, At, _transitions);
        session.MoveTo(SessionState.Active, At, _transitions);

        session.Participants.Should().BeEmpty();
        session.Teams.Should().OnlyContain(team => team.ActiveMemberCount == 0);
    }

    private static IReadOnlySet<Guid> Empty => new HashSet<Guid>();

    private static IReadOnlySet<Guid> SetOf(params Guid[] ids) => new HashSet<Guid>(ids);

    private static LiveSession CreateSession(int redCapacity, out Team red, out Team blue)
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        red = session.AssociateTeam(RedRef, "Red", "RED-01", redCapacity);
        blue = session.AssociateTeam(BlueRef, "Blue", "BLU-01", capacity: 4);
        return session;
    }
}
