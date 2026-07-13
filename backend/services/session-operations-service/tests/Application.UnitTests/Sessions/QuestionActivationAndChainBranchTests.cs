using umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation;
using umbral_backend.Application.Sessions.Common.TriviaAnswerValidation;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions;

// Covers SequentialQuestionActivationStrategy's index arithmetic across substage boundaries and the
// empty-chain short-circuit in TriviaAnswerValidationChain.
public sealed class QuestionActivationAndChainBranchTests
{
    private static readonly SequentialQuestionActivationStrategy Strategy = new();

    [Fact]
    public void Next_NoActiveSubstage_ReturnsNull()
    {
        // Scheduled session → ActiveSubstageId is null → questionCount 0 → null.
        var session = LiveSessionTestFactory.CreateScheduledTrivia(questionCount: 2);

        Strategy.Next(session).Should().BeNull();
    }

    [Fact]
    public void Next_FreshActiveSubstage_ReturnsZero()
    {
        var session = ActiveTrivia(questionCount: 2, activateIndex: null);

        Strategy.Next(session).Should().Be(0);
    }

    [Fact]
    public void Next_MoreQuestionsRemain_ReturnsNextIndex()
    {
        var session = ActiveTrivia(questionCount: 2, activateIndex: 0);

        // currentPosition 0 < count-1 (1) → advance to 1.
        Strategy.Next(session).Should().Be(1);
    }

    [Fact]
    public void Next_LastQuestion_ReturnsNull()
    {
        var session = ActiveTrivia(questionCount: 2, activateIndex: 1);

        // currentPosition 1 >= count-1 (1) → exhausted.
        Strategy.Next(session).Should().BeNull();
    }

    [Fact]
    public async Task Chain_WithNoLinks_CompletesWithoutInspectingContext()
    {
        var chain = new TriviaAnswerValidationChain(
            new EvidenceIntakeValidationChain([]),
            Array.Empty<TriviaAnswerValidationLink>());
        var context = new TriviaAnswerValidationContext(
            LiveSessionTestFactory.CreateScheduledTrivia(), Guid.NewGuid(), Guid.NewGuid(), 1, token: null,
            LiveSessionTestFactory.TriviaQuestionActivatedAt);

        await chain.ValidateAsync(context, CancellationToken.None);
    }

    private static Domain.Entities.LiveSession ActiveTrivia(int questionCount, int? activateIndex)
    {
        var session = LiveSessionTestFactory.CreateScheduledTrivia(questionCount: questionCount);
        var policy = new SessionStateTransitionPolicy();
        var at = LiveSessionTestFactory.TriviaQuestionActivatedAt;
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        session.MoveTo(SessionState.Preparing, at.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, at.AddSeconds(-30), policy);
        if (activateIndex is int index)
        {
            session.ActivateQuestion(index, at);
        }

        return session;
    }
}
