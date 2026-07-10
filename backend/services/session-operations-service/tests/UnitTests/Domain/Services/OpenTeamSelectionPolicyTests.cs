using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Services;

// Open Team Selection policy (#88): unassigned participants pick any attached team pre-start;
// whitelisted participants are confined to their set; selection closes once the session starts.
public sealed class OpenTeamSelectionPolicyTests
{
    private static readonly Guid RedRef = Guid.NewGuid();
    private static readonly Guid BlueRef = Guid.NewGuid();
    private static readonly Guid GreenRef = Guid.NewGuid();

    private readonly OpenTeamSelectionPolicy _policy = new();
    private readonly SessionStateTransitionPolicy _transitions = new();

    [Fact]
    public void SelectableTeams_WhenUnassigned_ReturnsAllAttachedTeams()
    {
        var session = CreateScheduledSessionWithTeams(out var red, out var blue, out _);

        var selectable = _policy.SelectableTeams(session, Empty);

        selectable.Should().BeEquivalentTo(new[] { red, blue, session.Teams.Single(t => t.ReferenceTeamId == GreenRef) });
    }

    [Fact]
    public void SelectableTeams_WhenWhitelisted_ReturnsOnlyWhitelistedTeams()
    {
        var session = CreateScheduledSessionWithTeams(out var red, out var blue, out _);

        var selectable = _policy.SelectableTeams(session, SetOf(RedRef, BlueRef));

        selectable.Should().BeEquivalentTo(new[] { red, blue });
    }

    [Fact]
    public void SelectableTeams_WhenRuntimeOnlyTeamExists_IgnoresItForWhitelistMatching()
    {
        var session = CreateScheduledSessionWithTeams(out var red, out _, out _);
        session.RegisterTeam("Walk-ins", "WLK-01", capacity: 4);

        var selectable = _policy.SelectableTeams(session, SetOf(RedRef));

        selectable.Should().ContainSingle().Which.Should().Be(red);
    }

    [Fact]
    public void SelectableTeams_WhenWhitelistTouchesNoAttachedTeam_TreatsParticipantAsUnassigned()
    {
        // Membership only for a team not attached to this session => "no membership for any attached
        // team" (decisions §7) => Open Team Selection over all attached teams.
        var session = CreateScheduledSessionWithTeams(out _, out _, out _);

        var selectable = _policy.SelectableTeams(session, SetOf(Guid.NewGuid()));

        selectable.Should().HaveCount(3);
    }

    [Fact]
    public void SelectableTeams_WhenSessionActive_ReturnsEmpty()
    {
        var session = CreateActiveSessionWithTeams();

        _policy.SelectableTeams(session, Empty).Should().BeEmpty();
    }

    [Theory]
    [InlineData(SessionState.Scheduled)]
    [InlineData(SessionState.Preparing)]
    public void EnsureCanSelfAssign_WhenUnassignedAndPreStart_Allows(SessionState state)
    {
        var session = CreateScheduledSessionWithTeams(out var red, out _, out _);
        if (state == SessionState.Preparing)
        {
            session.MoveTo(SessionState.Preparing, session.ScheduledAt, _transitions);
        }

        var act = () => _policy.EnsureCanSelfAssign(session, red, Empty);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureCanSelfAssign_WhenTargetOutsideWhitelist_Throws()
    {
        var session = CreateScheduledSessionWithTeams(out _, out _, out var green);

        var act = () => _policy.EnsureCanSelfAssign(session, green, SetOf(RedRef, BlueRef));

        act.Should().Throw<TeamNotInAuthorizedSetException>();
    }

    [Fact]
    public void EnsureCanSelfAssign_WhenTargetInWhitelist_Allows()
    {
        var session = CreateScheduledSessionWithTeams(out var red, out _, out _);

        var act = () => _policy.EnsureCanSelfAssign(session, red, SetOf(RedRef, BlueRef));

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureCanSelfAssign_WhenSessionStarted_Throws()
    {
        var session = CreateActiveSessionWithTeams();
        var red = session.Teams.Single(t => t.ReferenceTeamId == RedRef);

        var act = () => _policy.EnsureCanSelfAssign(session, red, Empty);

        act.Should().Throw<OpenTeamSelectionClosedException>();
    }

    private static IReadOnlySet<Guid> Empty => new HashSet<Guid>();

    private static IReadOnlySet<Guid> SetOf(params Guid[] ids) => new HashSet<Guid>(ids);

    private static LiveSession CreateScheduledSessionWithTeams(out Team red, out Team blue, out Team green)
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        red = session.AssociateTeam(RedRef, "Red", "RED-01", capacity: 4);
        blue = session.AssociateTeam(BlueRef, "Blue", "BLU-01", capacity: 4);
        green = session.AssociateTeam(GreenRef, "Green", "GRN-01", capacity: 4);
        return session;
    }

    private LiveSession CreateActiveSessionWithTeams()
    {
        var session = CreateScheduledSessionWithTeams(out _, out _, out _);
        session.MoveTo(SessionState.Preparing, session.ScheduledAt, _transitions);
        session.MoveTo(SessionState.Active, session.ScheduledAt, _transitions);
        return session;
    }
}
