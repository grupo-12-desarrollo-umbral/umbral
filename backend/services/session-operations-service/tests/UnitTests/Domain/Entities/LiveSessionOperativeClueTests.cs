using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

public sealed class LiveSessionOperativeClueTests
{
    private const string OperativeClueCreatedEventType = "OperativeClueCreated";
    private static readonly DateTimeOffset CreatedAt = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddOperativeClue_FansOutTrimmedRecordAndEventPerAssignedTeam()
    {
        var session = CreateActiveSession(out var alpha, out var bravo);

        session.AddOperativeClue("  Check beneath the clock.  ", [alpha.TeamId, bravo.TeamId], 42, CreatedAt);

        var clues = session.GetOperativeClues();
        clues.Should().HaveCount(2);
        clues.Select(clue => clue.TeamId).Should().BeEquivalentTo([alpha.TeamId, bravo.TeamId]);
        clues.Should().AllSatisfy(clue =>
        {
            clue.OperativeClueId.Should().NotBeEmpty();
            clue.LiveSessionId.Should().Be(session.LiveSessionId);
            clue.ClueText.Should().Be("Check beneath the clock.");
            clue.CreatedByUserId.Should().Be(42);
            clue.CreatedAt.Should().Be(CreatedAt);
        });

        var events = session.DomainEvents.OfType<OperativeClueAddedEvent>().ToArray();
        events.Should().HaveCount(2);
        events.Select(domainEvent => domainEvent.TeamId).Should().BeEquivalentTo([alpha.TeamId, bravo.TeamId]);
        events.Should().AllSatisfy(domainEvent =>
        {
            domainEvent.OperativeClueId.Should().NotBeEmpty();
            domainEvent.LiveSessionId.Should().Be(session.LiveSessionId);
            domainEvent.ClueText.Should().Be("Check beneath the clock.");
            domainEvent.CreatedByUserId.Should().Be(42);
            domainEvent.CreatedAt.Should().Be(CreatedAt);
        });

        var sessionEvents = session.SessionEvents
            .Where(se => se.EventType == OperativeClueCreatedEventType)
            .ToArray();
        sessionEvents.Should().HaveCount(2);
        sessionEvents.Select(se => se.ActorId).Should().AllBeEquivalentTo(42);
        sessionEvents.Select(se => se.ActorType).Should().AllBeEquivalentTo(SessionEventActorType.Operator);
        sessionEvents.Select(se => se.OccurredAt).Should().AllBeEquivalentTo(CreatedAt);
        sessionEvents.Should().AllSatisfy(se =>
        {
            se.SessionEventId.Should().NotBeEmpty();
            se.LiveSessionId.Should().Be(session.LiveSessionId);
            se.CorrelationId.Should().NotBeEmpty();
            se.PayloadSummary.Should().Contain("Check beneath the clock.");
        });
    }

    [Theory]
    [InlineData(SessionState.Scheduled)]
    [InlineData(SessionState.Preparing)]
    [InlineData(SessionState.Finished)]
    [InlineData(SessionState.Cancelled)]
    public void AddOperativeClue_WhenSessionIsNotActiveOrPaused_Rejects(SessionState state)
    {
        var session = CreateSessionInState(state, out var team);

        var act = () => session.AddOperativeClue("Look up.", [team.TeamId], 42, CreatedAt);

        act.Should().Throw<SessionNotLiveForOperativeClueException>()
            .Which.Category.Should().Be(ErrorCategory.Conflict);
        session.GetOperativeClues().Should().BeEmpty();
        session.SessionEvents.Where(se => se.EventType == OperativeClueCreatedEventType).Should().BeEmpty();
    }

    [Fact]
    public void AddOperativeClue_WhenSessionIsPaused_IsAllowed()
    {
        var session = CreateActiveSession(out var alpha, out _);
        session.MoveTo(SessionState.Paused, CreatedAt.AddMinutes(-1), new SessionStateTransitionPolicy());

        session.AddOperativeClue("Look up.", [alpha.TeamId], 42, CreatedAt);

        session.GetOperativeClues().Should().ContainSingle();
        session.SessionEvents.Where(se => se.EventType == OperativeClueCreatedEventType).Should().ContainSingle();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddOperativeClue_WhenTextIsMissing_Rejects(string clueText)
    {
        var session = CreateActiveSession(out var alpha, out _);

        var act = () => session.AddOperativeClue(clueText, [alpha.TeamId], 42, CreatedAt);

        act.Should().Throw<OperativeClueTextRequiredException>()
            .Which.Category.Should().Be(ErrorCategory.Validation);
        session.GetOperativeClues().Should().BeEmpty();
        session.SessionEvents.Where(se => se.EventType == OperativeClueCreatedEventType).Should().BeEmpty();
    }

    [Fact]
    public void AddOperativeClue_WhenNoTeamIsAssigned_Rejects()
    {
        var session = CreateActiveSession(out _, out _);

        var act = () => session.AddOperativeClue("Look up.", [], 42, CreatedAt);

        act.Should().Throw<OperativeClueRequiresAtLeastOneTeamException>()
            .Which.Category.Should().Be(ErrorCategory.Validation);
        session.GetOperativeClues().Should().BeEmpty();
        session.SessionEvents.Where(se => se.EventType == OperativeClueCreatedEventType).Should().BeEmpty();
    }

    [Fact]
    public void AddOperativeClue_WhenAnyTeamIsUnknown_RejectsAtomically()
    {
        var session = CreateActiveSession(out var alpha, out _);
        var unknownTeamId = Guid.NewGuid();

        var act = () => session.AddOperativeClue("Look up.", [alpha.TeamId, unknownTeamId], 42, CreatedAt);

        act.Should().Throw<TeamNotFoundException>();
        session.GetOperativeClues().Should().BeEmpty();
        session.DomainEvents.OfType<OperativeClueAddedEvent>().Should().BeEmpty();
        session.SessionEvents.Where(se => se.EventType == OperativeClueCreatedEventType).Should().BeEmpty();
    }

    [Fact]
    public void AddOperativeClue_SurfacesOnlyForAssignedTeam()
    {
        var session = CreateActiveSession(out var alpha, out var bravo);

        session.AddOperativeClue("Check beneath the clock.", [alpha.TeamId], 42, CreatedAt);

        var operativeClue = session.GetOperativeClues().Single();
        var projectedClue = session.ProjectParticipantTeamBoard(alpha.TeamId, CreatedAt).VisibleClues
            .Should().ContainSingle(clue => clue.ClueText == "Check beneath the clock.")
            .Which;
        projectedClue.OperativeClueId.Should().Be(operativeClue.OperativeClueId);
        projectedClue.TargetSnapshotId.Should().BeNull();
        projectedClue.TargetName.Should().BeNull();
        session.ProjectParticipantTeamBoard(bravo.TeamId, CreatedAt).VisibleClues
            .Should().NotContain(clue => clue.ClueText == "Check beneath the clock.");
    }

    [Fact]
    public void ProjectParticipantTeamBoard_OrdersOperativeCluesNewestAuthoredFirst()
    {
        var session = CreateActiveSession(out var alpha, out _);

        session.AddOperativeClue("Oldest.", [alpha.TeamId], 42, CreatedAt);
        session.AddOperativeClue("Middle.", [alpha.TeamId], 42, CreatedAt.AddMinutes(1));
        session.AddOperativeClue("Newest.", [alpha.TeamId], 42, CreatedAt.AddMinutes(2));

        session.ProjectParticipantTeamBoard(alpha.TeamId, CreatedAt.AddMinutes(3)).VisibleClues
            .Where(clue => clue.OperativeClueId is not null)
            .Select(clue => clue.ClueText)
            .Should().ContainInOrder("Newest.", "Middle.", "Oldest.");
    }

    [Fact]
    public void ProjectParticipantTeamBoard_OrdersOperativeCluesByAuthoredInstantNotInsertionOrder()
    {
        // The owned collection is loaded without an ORDER BY, so insertion order is not a contract:
        // ordering must hold even when the clues land out of chronological sequence.
        var session = CreateActiveSession(out var alpha, out _);

        session.AddOperativeClue("Authored second.", [alpha.TeamId], 42, CreatedAt.AddMinutes(1));
        session.AddOperativeClue("Authored first.", [alpha.TeamId], 42, CreatedAt);

        session.ProjectParticipantTeamBoard(alpha.TeamId, CreatedAt.AddMinutes(2)).VisibleClues
            .Where(clue => clue.OperativeClueId is not null)
            .Select(clue => clue.ClueText)
            .Should().ContainInOrder("Authored second.", "Authored first.");
    }

    [Fact]
    public void ProjectParticipantTeamBoard_KeepsOperativeClueOrderingScopedToTheTeam()
    {
        // An all-teams push stamps every team's clue with the same instant; each team still sees only
        // its own clues, newest-first, unaffected by the other team's records.
        var session = CreateActiveSession(out var alpha, out var bravo);

        session.AddOperativeClue("Broadcast.", [alpha.TeamId, bravo.TeamId], 42, CreatedAt);
        session.AddOperativeClue("Alpha only, later.", [alpha.TeamId], 42, CreatedAt.AddMinutes(1));

        session.ProjectParticipantTeamBoard(alpha.TeamId, CreatedAt.AddMinutes(2)).VisibleClues
            .Where(clue => clue.OperativeClueId is not null)
            .Select(clue => clue.ClueText)
            .Should().ContainInOrder("Alpha only, later.", "Broadcast.");
        session.ProjectParticipantTeamBoard(bravo.TeamId, CreatedAt.AddMinutes(2)).VisibleClues
            .Where(clue => clue.OperativeClueId is not null)
            .Select(clue => clue.ClueText)
            .Should().Equal("Broadcast.");
    }

    [Fact]
    public void AddOperativeClue_DoesNotAdvanceSubstageOrResolveTarget()
    {
        var session = CreateActiveSession(out var alpha, out _);
        var activeSubstageId = session.ActiveSubstageId;

        session.AddOperativeClue("Check beneath the clock.", [alpha.TeamId], 42, CreatedAt);

        session.ActiveSubstageId.Should().Be(activeSubstageId);
        session.ProjectParticipantTeamBoard(alpha.TeamId, CreatedAt)
            .ActiveSubstageContext!.ResolvedTargets.Should().Be(0);
        session.DomainEvents.OfType<SubstageAdvancedEvent>().Should().BeEmpty();
    }

    private static LiveSession CreateActiveSession(out Team alpha, out Team bravo)
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        alpha = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        bravo = session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-01", 4);
        MoveToActive(session);
        return session;
    }

    private static LiveSession CreateSessionInState(SessionState state, out Team team)
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();

        if (state is not SessionState.Scheduled)
        {
            session.MoveTo(SessionState.Preparing, CreatedAt.AddMinutes(-4), policy);
        }

        if (state is SessionState.Finished or SessionState.Cancelled)
        {
            session.MoveTo(SessionState.Active, CreatedAt.AddMinutes(-3), policy);
        }

        if (state is SessionState.Finished)
        {
            session.MoveTo(SessionState.Finished, CreatedAt.AddMinutes(-2), policy);
        }
        else if (state is SessionState.Cancelled)
        {
            session.MoveTo(SessionState.Cancelled, CreatedAt.AddMinutes(-2), policy);
        }

        return session;
    }

    private static void MoveToActive(LiveSession session)
    {
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, CreatedAt.AddMinutes(-2), policy);
        session.MoveTo(SessionState.Active, CreatedAt.AddMinutes(-1), policy);
    }
}
