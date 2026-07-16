using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Queries.GetTeamTriviaQuestionResult;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Queries.GetTeamTriviaQuestionResult;

public sealed class GetTeamTriviaQuestionResultQueryHandlerTests
{
    private static readonly DateTimeOffset ActivatedAt = LiveSessionTestFactory.TriviaQuestionActivatedAt;

    [Fact]
    public async Task Handle_ReturnsCallersTeamAnswerAndRevealAfterClose()
    {
        var session = CreateSessionWithParticipant(out var externalIdentityId, questionCount: 2);
        var participant = session.Participants.Single(candidate => candidate.ExternalIdentityId == externalIdentityId);
        var team = session.FindTeamForExternalParticipant(externalIdentityId)!;
        session.RegisterTriviaAnswer(
            team.TeamId,
            selectedOptionSequenceOrder: 1,
            participant.SessionParticipantId,
            ActivatedAt.AddSeconds(5));
        CloseFirstAndActivateSecond(session);
        var handler = CreateHandler(session, externalIdentityId);

        var result = await handler.Handle(
            new GetTeamTriviaQuestionResultQuery(session.LiveSessionId, 1),
            CancellationToken.None);

        result.SelectedOptionSequenceOrder.Should().Be(1);
        result.IsCorrect.Should().BeTrue();
        result.ScoreValue.Should().Be(100);
        result.CorrectOptionSequenceOrder.Should().Be(1);
        result.Explanation.Should().Be("Mercury is the closest planet.");
    }

    [Fact]
    public async Task Handle_WhenTeamDidNotAnswer_ReturnsNullAnswerFieldsAndReveal()
    {
        var session = CreateSessionWithParticipant(out var externalIdentityId, questionCount: 2);
        CloseFirstAndActivateSecond(session);
        var handler = CreateHandler(session, externalIdentityId);

        var result = await handler.Handle(
            new GetTeamTriviaQuestionResultQuery(session.LiveSessionId, 1),
            CancellationToken.None);

        result.SelectedOptionSequenceOrder.Should().BeNull();
        result.IsCorrect.Should().BeNull();
        result.ScoreValue.Should().BeNull();
        result.CorrectOptionSequenceOrder.Should().Be(1);
        result.Explanation.Should().Be("Mercury is the closest planet.");
    }

    [Fact]
    public async Task Handle_WhenQuestionIsStillActive_RejectsReveal()
    {
        var session = CreateSessionWithParticipant(out var externalIdentityId, questionCount: 1);
        var handler = CreateHandler(session, externalIdentityId);

        var act = async () => await handler.Handle(
            new GetTeamTriviaQuestionResultQuery(session.LiveSessionId, 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<TriviaQuestionResultNotAvailableException>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotOnSessionTeam_ThrowsForbidden()
    {
        var session = CreateSessionWithParticipant(out _, questionCount: 2);
        CloseFirstAndActivateSecond(session);
        var handler = CreateHandler(session, Guid.NewGuid());

        var act = async () => await handler.Handle(
            new GetTeamTriviaQuestionResultQuery(session.LiveSessionId, 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    private static LiveSession CreateSessionWithParticipant(
        out Guid externalIdentityId,
        int questionCount)
    {
        var session = LiveSessionTestFactory.CreateScheduledTrivia(questionCount: questionCount);
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, ActivatedAt.AddMinutes(-1), policy);
        externalIdentityId = Guid.NewGuid();
        session.AdmitParticipant(
            externalIdentityId,
            "Alice",
            team.TeamId,
            ActivatedAt.AddSeconds(-45),
            new JoinPolicy());
        session.MoveTo(SessionState.Active, ActivatedAt.AddSeconds(-30), policy);
        session.ActivateQuestion(0, ActivatedAt);
        return session;
    }

    private static void CloseFirstAndActivateSecond(LiveSession session)
    {
        session.CloseActiveQuestion(ActivatedAt.AddSeconds(30));
        session.ActivateQuestion(1, ActivatedAt.AddSeconds(30));
    }

    private static GetTeamTriviaQuestionResultQueryHandler CreateHandler(
        LiveSession session,
        Guid externalIdentityId)
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(candidate => candidate.GetByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(candidate => candidate.Id).Returns(externalIdentityId.ToString());
        return new GetTeamTriviaQuestionResultQueryHandler(repository.Object, currentUser.Object);
    }
}
