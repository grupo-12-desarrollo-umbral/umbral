using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

public sealed class LiveSessionTests
{
    private readonly JoinPolicy _joinPolicy = new();

    [Fact]
    public void CreateTrivia_WithMismatchedSource_ThrowsException()
    {
        var act = () => LiveSession.CreateTrivia(
            SessionSource.Create(SessionSourceType.Mission, Guid.NewGuid()),
            "abc123",
            "Trivia Session",
            30,
            DateTimeOffset.UtcNow,
            TriviaSessionSnapshotFactory.CreateSingleQuestion());

        act.Should().Throw<SessionSourceDoesNotMatchModeException>();
    }

    [Fact]
    public void Create_WithTriviaModeWithoutSnapshot_ThrowsException()
    {
        var act = () => LiveSession.Create(
            SessionMode.Trivia,
            SessionSource.CreateTriviaQuiz(42),
            "tri-123",
            "Trivia Session",
            30,
            DateTimeOffset.UtcNow);

        act.Should().Throw<TriviaSessionSnapshotRequiredException>();
    }

    [Fact]
    public void CreateTrivia_WithSnapshot_AttachesFixedCopy()
    {
        var snapshot = TriviaSessionSnapshotFactory.CreateSingleQuestion();

        var session = LiveSession.CreateTrivia(
            SessionSource.CreateTriviaQuiz(42),
            "tri-123",
            "Trivia Session",
            30,
            DateTimeOffset.UtcNow,
            snapshot);

        session.SessionMode.Should().Be(SessionMode.Trivia);
        session.Source.SourceTriviaQuizId.Should().Be(42);
        session.TriviaSnapshot.Should().NotBeNull();
        session.TriviaSnapshot!.Questions.Should().ContainSingle();
        session.TriviaSnapshot.QuizTitle.Should().Be("Foundations of Science");
    }

    [Fact]
    public void RegisterTeam_WithDuplicateCode_ThrowsException()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        session.RegisterTeam("Alpha", "alpha", 4);

        var act = () => session.RegisterTeam("Beta", "ALPHA", 4);

        act.Should().Throw<DuplicateTeamCodeInSessionException>();
    }

    [Fact]
    public void RegisterTeam_WithExplicitTeamId_PreservesIdentityReferenceTeamId()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var identityReferenceTeamId = Guid.NewGuid();

        var team = session.RegisterTeam(identityReferenceTeamId, "Alpha", "A-01", 4);

        team.TeamId.Should().Be(identityReferenceTeamId);
        session.Teams.Should().ContainSingle().Which.TeamId.Should().Be(identityReferenceTeamId);
    }

    [Fact]
    public void RegisterTeam_WithEmptyTeamId_ThrowsException()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();

        var act = () => session.RegisterTeam(Guid.Empty, "Alpha", "A-01", 4);

        act.Should().Throw<TeamIdentityRequiredException>();
    }

    [Fact]
    public void AdmitParticipant_WhenNewParticipant_CreatesMembership()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var team = session.RegisterTeam("Alpha", "A-01", 4);

        var result = session.AdmitParticipant(Guid.NewGuid(), "Nora", team.TeamId, DateTimeOffset.UtcNow, _joinPolicy);

        result.IsReconnect.Should().BeFalse();
        result.Participant.ParticipantStatus.Should().Be(ParticipantStatus.Active);
        result.Team.Members.Should().ContainSingle();
    }

    [Fact]
    public void AdmitParticipant_WhenParticipantReconnects_RestoresPresenceInAssignedTeam()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var team = session.RegisterTeam("Alpha", "A-01", 4);
        var identityId = Guid.NewGuid();
        var joinedAt = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);
        var joined = session.AdmitParticipant(identityId, "Nora", team.TeamId, joinedAt, _joinPolicy);
        session.DisconnectParticipant(joined.Participant.SessionParticipantId, joinedAt.AddMinutes(5));
        session.MoveTo(SessionState.Preparing, joinedAt.AddMinutes(6), new SessionStateTransitionPolicy());

        var reconnected = session.AdmitParticipant(identityId, "Nora", team.TeamId, joinedAt.AddMinutes(7), _joinPolicy);

        reconnected.IsReconnect.Should().BeTrue();
        reconnected.Team.TeamId.Should().Be(team.TeamId);
        reconnected.Participant.ParticipantStatus.Should().Be(ParticipantStatus.Active);
    }

    [Fact]
    public void AdmitParticipant_WhenSessionIsActive_RejectsLateJoin()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var team = session.RegisterTeam("Alpha", "A-01", 4);
        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, DateTimeOffset.UtcNow, transitionPolicy);
        session.MoveTo(SessionState.Active, DateTimeOffset.UtcNow.AddMinutes(1), transitionPolicy);

        var act = () => session.AdmitParticipant(Guid.NewGuid(), "Late", team.TeamId, DateTimeOffset.UtcNow.AddMinutes(2), _joinPolicy);

        act.Should().Throw<LateJoinNotAllowedException>();
    }

    [Fact]
    public void Create_WithBlankSessionCode_ThrowsException()
    {
        var act = () => LiveSession.Create(
            SessionMode.TreasureHunt,
            SessionSource.Create(SessionSourceType.Mission, Guid.NewGuid()),
            " ",
            "Treasure Session",
            30,
            DateTimeOffset.UtcNow);

        act.Should().Throw<LiveSessionCodeRequiredException>();
    }

    [Fact]
    public void Create_WithBlankTitle_ThrowsException()
    {
        var act = () => LiveSession.Create(
            SessionMode.TreasureHunt,
            SessionSource.Create(SessionSourceType.Mission, Guid.NewGuid()),
            "ses-123",
            " ",
            30,
            DateTimeOffset.UtcNow);

        act.Should().Throw<LiveSessionTitleRequiredException>();
    }

    [Fact]
    public void OpenJoinContext_AddsPendingJoinContext()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var createdAt = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);

        var joinContext = session.OpenJoinContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            createdAt,
            createdAt.AddMinutes(5));

        session.JoinContexts.Should().ContainSingle().Which.Should().Be(joinContext);
        joinContext.Status.Should().Be(JoinContextStatus.Pending);
    }

    [Fact]
    public void MoveTo_TracksLifecycleTimestampsAndReason()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        session.RegisterTeam("Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        var preparingAt = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);
        var activeAt = preparingAt.AddMinutes(1);
        var pausedAt = activeAt.AddMinutes(5);
        var finishedAt = pausedAt.AddMinutes(3);

        session.MoveTo(SessionState.Preparing, preparingAt, policy, "  ready  ");
        session.MoveTo(SessionState.Active, activeAt, policy);
        session.MoveTo(SessionState.Paused, pausedAt, policy);
        session.MoveTo(SessionState.Finished, finishedAt, policy, "  complete  ");

        session.StartedAt.Should().Be(activeAt);
        session.PausedAt.Should().Be(pausedAt);
        session.EndedAt.Should().Be(finishedAt);
        session.StateReason.Should().Be("complete");
        session.LastStateChangedAt.Should().Be(finishedAt);
    }
}
