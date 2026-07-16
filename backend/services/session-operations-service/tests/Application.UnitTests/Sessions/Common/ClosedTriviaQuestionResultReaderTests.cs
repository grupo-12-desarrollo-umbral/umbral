using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Common;

public sealed class ClosedTriviaQuestionResultReaderTests
{
    private static readonly DateTimeOffset Now = LiveSessionTestFactory.TriviaQuestionActivatedAt;

    [Fact]
    public void GetClosedQuestion_WhenSessionIsNull_Throws()
    {
        var act = () => ClosedTriviaQuestionResultReader.GetClosedQuestion(null!, 1);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(SessionState.Scheduled)]
    [InlineData(SessionState.Preparing)]
    [InlineData(SessionState.Cancelled)]
    public void GetClosedQuestion_WhenSessionCannotHaveAClosedResult_Rejects(SessionState state)
    {
        var session = LiveSessionTestFactory.CreateScheduledTrivia();
        var policy = new SessionStateTransitionPolicy();
        if (state == SessionState.Preparing)
        {
            session.MoveTo(SessionState.Preparing, Now, policy);
        }
        else if (state == SessionState.Cancelled)
        {
            session.MoveTo(SessionState.Cancelled, Now, policy);
        }

        var act = () => ClosedTriviaQuestionResultReader.GetClosedQuestion(session, 1);

        act.Should().Throw<TriviaQuestionResultNotAvailableException>();
    }

    [Fact]
    public void GetClosedQuestion_WhenActiveTriviaQuestionDoesNotExist_Rejects()
    {
        var session = CreateActiveTrivia(questionCount: 2);

        var act = () => ClosedTriviaQuestionResultReader.GetClosedQuestion(session, 99);

        act.Should().Throw<TriviaQuestionResultNotAvailableException>();
    }

    [Fact]
    public void GetClosedQuestion_DuringCloseToAdvanceGap_Rejects()
    {
        var session = CreateActiveTrivia(questionCount: 2);
        session.CloseActiveQuestion(Now.AddSeconds(30));

        var act = () => ClosedTriviaQuestionResultReader.GetClosedQuestion(session, 1);

        act.Should().Throw<TriviaQuestionResultNotAvailableException>();
    }

    [Fact]
    public void GetClosedQuestion_DuringRevealWindow_MidSubstage_ReturnsJustClosedQuestion()
    {
        var session = CreateActiveTrivia(questionCount: 2);
        var nextQuestionIndex = new SequentialQuestionActivationStrategy().Next(session);
        session.CloseActiveQuestionForReveal(Now.AddSeconds(30), TimeSpan.FromSeconds(5), nextQuestionIndex);

        var result = ClosedTriviaQuestionResultReader.GetClosedQuestion(session, 1);

        result.SequenceOrder.Should().Be(1);
    }

    [Fact]
    public void GetClosedQuestion_DuringRevealWindow_MidSubstage_RejectsNotYetActivatedNextQuestion()
    {
        var session = CreateActiveTrivia(questionCount: 2);
        var nextQuestionIndex = new SequentialQuestionActivationStrategy().Next(session);
        session.CloseActiveQuestionForReveal(Now.AddSeconds(30), TimeSpan.FromSeconds(5), nextQuestionIndex);

        // Question 2 has not been activated yet — the reveal window exposes only the just-closed question.
        var act = () => ClosedTriviaQuestionResultReader.GetClosedQuestion(session, 2);

        act.Should().Throw<TriviaQuestionResultNotAvailableException>();
    }

    [Fact]
    public void GetClosedQuestion_DuringRevealWindow_LastQuestion_ReturnsJustClosedQuestion()
    {
        var session = CreateActiveTrivia(questionCount: 1);
        var nextQuestionIndex = new SequentialQuestionActivationStrategy().Next(session);
        nextQuestionIndex.Should().BeNull("the substage's last question just closed");
        session.CloseActiveQuestionForReveal(Now.AddSeconds(30), TimeSpan.FromSeconds(5), nextQuestionIndex);

        var result = ClosedTriviaQuestionResultReader.GetClosedQuestion(session, 1);

        result.SequenceOrder.Should().Be(1);
        result.Explanation.Should().Be("Mercury is the closest planet.");
    }

    [Fact]
    public void GetClosedQuestion_AfterSessionFinishes_ReturnsFinalQuestion()
    {
        var session = CreateActiveTrivia(questionCount: 1);
        session.CloseActiveQuestion(Now.AddSeconds(30));
        session.CompleteActiveSubstageAndAdvance(Now.AddSeconds(30), new SessionStateTransitionPolicy());

        var result = ClosedTriviaQuestionResultReader.GetClosedQuestion(session, 1);

        result.SequenceOrder.Should().Be(1);
        result.Explanation.Should().Be("Mercury is the closest planet.");
    }

    [Fact]
    public void GetClosedQuestion_InLaterTreasureSubstage_ReturnsMostRecentCompletedTriviaQuestion()
    {
        var session = LiveSessionTestFactory.CreateScheduledTriviaThenTreasureHunt();
        ActivateFirstQuestion(session);
        session.CloseActiveQuestion(Now.AddSeconds(30));
        session.CompleteActiveSubstageAndAdvance(Now.AddSeconds(30), new SessionStateTransitionPolicy());

        var result = ClosedTriviaQuestionResultReader.GetClosedQuestion(session, 1);

        result.SequenceOrder.Should().Be(1);
    }

    [Fact]
    public void GetClosedQuestion_InFirstTreasureSubstage_RejectsMissingCompletedTriviaQuestion()
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt();
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, Now, policy);

        var act = () => ClosedTriviaQuestionResultReader.GetClosedQuestion(session, 1);

        act.Should().Throw<TriviaQuestionResultNotAvailableException>();
    }

    private static LiveSession CreateActiveTrivia(int questionCount)
    {
        var session = LiveSessionTestFactory.CreateScheduledTrivia(questionCount: questionCount);
        ActivateFirstQuestion(session);
        return session;
    }

    private static void ActivateFirstQuestion(LiveSession session)
    {
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, Now.AddSeconds(-30), policy);
        session.ActivateQuestion(0, Now);
    }
}
