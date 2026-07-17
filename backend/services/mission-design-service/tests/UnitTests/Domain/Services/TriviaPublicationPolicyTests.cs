using System.Reflection;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Domain.Services;

public class TriviaPublicationPolicyTests
{
    [Fact]
    public void EnsurePublishable_WhenQuizSatisfiesTriviaReadiness_AllowsPublication()
    {
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");
        quiz.AddQuestion(
            "Capital of France?",
            100,
            45,
            null,
            [
                TriviaOption.Create("Paris", 1, true),
                TriviaOption.Create("Berlin", 2, false)
            ]);

        var act = () => TriviaPublicationPolicy.EnsurePublishable(quiz);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsurePublishable_WhenQuestionHasNoScoreValue_RejectsQuiz()
    {
        var quiz = TriviaQuiz.Create(
            "Intro Quiz",
            "Warm-up trivia",
            [
                TriviaQuestion.Create(
                    "Question 1",
                    null,
                    45,
                    null,
                    [
                        TriviaOption.Create("Option A", 1, true),
                        TriviaOption.Create("Option B", 2, false)
                    ])
            ]);

        var act = () => TriviaPublicationPolicy.EnsurePublishable(quiz);

        act.Should().Throw<TriviaQuestionScoreValueRequiredToPublishException>();
    }

    [Fact]
    public void EnsurePublishable_WhenQuestionHasFewerThanTwoOptions_RejectsQuiz()
    {
        var quiz = MaterializePersistedQuiz(
            TriviaQuestion.Create(
                "Question 1",
                100,
                45,
                null,
                [TriviaOption.Create("Only option", 1, true)]));

        var act = () => TriviaPublicationPolicy.EnsurePublishable(quiz);

        act.Should().Throw<TriviaQuestionMustHaveBetweenTwoAndFourOptionsException>();
    }

    [Fact]
    public void EnsurePublishable_WhenQuestionHasMoreThanFourOptions_RejectsQuiz()
    {
        var quiz = MaterializePersistedQuiz(
            TriviaQuestion.Create(
                "Question 1",
                100,
                45,
                null,
                [
                    TriviaOption.Create("Option A", 1, true),
                    TriviaOption.Create("Option B", 2, false),
                    TriviaOption.Create("Option C", 3, false),
                    TriviaOption.Create("Option D", 4, false),
                    TriviaOption.Create("Option E", 5, false)
                ]));

        var act = () => TriviaPublicationPolicy.EnsurePublishable(quiz);

        act.Should().Throw<TriviaQuestionMustHaveBetweenTwoAndFourOptionsException>();
    }

    [Fact]
    public void EnsurePublishable_WhenQuestionHasNoCorrectOption_RejectsQuiz()
    {
        var quiz = MaterializePersistedQuiz(
            TriviaQuestion.Create(
                "Question 1",
                100,
                45,
                null,
                [
                    TriviaOption.Create("Option A", 1, false),
                    TriviaOption.Create("Option B", 2, false)
                ]));

        var act = () => TriviaPublicationPolicy.EnsurePublishable(quiz);

        act.Should().Throw<TriviaQuestionMustHaveExactlyOneCorrectOptionException>();
    }

    [Fact]
    public void EnsurePublishable_WhenQuizHasNoQuestions_RejectsQuiz()
    {
        var quiz = TriviaQuiz.Create("Empty Quiz", "No questions yet");

        var act = () => TriviaPublicationPolicy.EnsurePublishable(quiz);

        act.Should().Throw<TriviaQuizMustHaveAtLeastOneQuestionToPublishException>();
    }

    [Fact]
    public void EnsurePublishable_WhenQuestionHasNoTimeLimit_RejectsQuiz()
    {
        var quiz = TriviaQuiz.Create(
            "Intro Quiz",
            "Warm-up trivia",
            [
                TriviaQuestion.Create(
                    "Question 1",
                    100,
                    null,
                    null,
                    [
                        TriviaOption.Create("Option A", 1, true),
                        TriviaOption.Create("Option B", 2, false)
                    ])
            ]);

        var act = () => TriviaPublicationPolicy.EnsurePublishable(quiz);

        act.Should().Throw<TriviaQuestionTimeLimitRequiredToPublishException>();
    }

    // Option-count and correct-count are now enforced at authoring time (TriviaQuiz.Create /
    // UpdateDetails / question authoring), so a malformed question can no longer be assembled
    // through the public API. The publication policy keeps its checks as defense-in-depth against
    // persisted data, so materialize the quiz through the private constructor EF uses to exercise
    // that defensive path with the malformed state stored data could still carry.
    private static TriviaQuiz MaterializePersistedQuiz(params TriviaQuestion[] questions)
    {
        var constructor = typeof(TriviaQuiz).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [
                typeof(string),
                typeof(string),
                typeof(TriviaQuizStatus),
                typeof(DateTimeOffset?),
                typeof(IEnumerable<TriviaQuestion>),
                typeof(int?),
                typeof(bool)
            ],
            modifiers: null)!;

        return (TriviaQuiz)constructor.Invoke(
            ["Intro Quiz", "Warm-up trivia", TriviaQuizStatus.Draft, null, questions, null, false]);
    }
}
