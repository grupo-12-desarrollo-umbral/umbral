using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Queries.GetOperatorTriviaAnswerReview;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Queries.GetOperatorTriviaAnswerReview;

public sealed class GetOperatorTriviaAnswerReviewQueryHandlerTests
{
    private static readonly DateTimeOffset ActivatedAt = LiveSessionTestFactory.TriviaQuestionActivatedAt;

    [Fact]
    public async Task Handle_ReturnsAnsweredAndNeverAnsweredTeamsAfterClose()
    {
        var session = LiveSessionTestFactory.CreateScheduledTrivia(questionCount: 2);
        var answeredTeam = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var unansweredTeam = session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-02", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, ActivatedAt.AddMinutes(-1), policy);
        var participant = session.AdmitParticipant(
            Guid.NewGuid(),
            "Alice",
            answeredTeam.TeamId,
            ActivatedAt.AddSeconds(-45),
            new JoinPolicy());
        session.MoveTo(SessionState.Active, ActivatedAt.AddSeconds(-30), policy);
        session.ActivateQuestion(0, ActivatedAt);
        var answeredAt = ActivatedAt.AddSeconds(4);
        session.RegisterTriviaAnswer(
            answeredTeam.TeamId,
            selectedOptionSequenceOrder: 2,
            participant.Participant.SessionParticipantId,
            answeredAt);
        session.CloseActiveQuestion(ActivatedAt.AddSeconds(30));
        session.ActivateQuestion(1, ActivatedAt.AddSeconds(30));
        var handler = CreateHandler(session);

        var result = await handler.Handle(
            new GetOperatorTriviaAnswerReviewQuery(session.LiveSessionId, 1),
            CancellationToken.None);

        result.LiveSessionId.Should().Be(session.LiveSessionId);
        result.QuestionSequenceOrder.Should().Be(1);
        result.Teams.Should().HaveCount(2);

        var answered = result.Teams.Single(team => team.TeamId == answeredTeam.TeamId);
        answered.TeamCode.Should().Be("A-01");
        answered.SelectedOptionSequenceOrder.Should().Be(2);
        answered.IsCorrect.Should().BeFalse();
        answered.ScoreValue.Should().Be(0);
        answered.AnsweredAt.Should().Be(answeredAt);

        var unanswered = result.Teams.Single(team => team.TeamId == unansweredTeam.TeamId);
        unanswered.SelectedOptionSequenceOrder.Should().BeNull();
        unanswered.IsCorrect.Should().BeNull();
        unanswered.ScoreValue.Should().BeNull();
        unanswered.AnsweredAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenResolverRejectsNonOwner_PropagatesForbidden()
    {
        var liveSessionId = Guid.NewGuid();
        var resolver = new Mock<ISessionAdministrationAccessResolver>();
        resolver
            .Setup(candidate => candidate.GetAuthorizedSessionAsync(
                liveSessionId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenAccessException());
        var handler = new GetOperatorTriviaAnswerReviewQueryHandler(resolver.Object);

        var act = async () => await handler.Handle(
            new GetOperatorTriviaAnswerReviewQuery(liveSessionId, 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Handle_WhenQuestionIsStillActive_RejectsReview()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(
            out _,
            out _);
        var handler = CreateHandler(session);

        var act = async () => await handler.Handle(
            new GetOperatorTriviaAnswerReviewQuery(session.LiveSessionId, 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<TriviaQuestionResultNotAvailableException>();
    }

    private static GetOperatorTriviaAnswerReviewQueryHandler CreateHandler(LiveSession session)
    {
        var resolver = new Mock<ISessionAdministrationAccessResolver>();
        resolver
            .Setup(candidate => candidate.GetAuthorizedSessionAsync(
                session.LiveSessionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        return new GetOperatorTriviaAnswerReviewQueryHandler(resolver.Object);
    }
}
