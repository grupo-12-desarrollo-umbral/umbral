using umbral_backend.Domain.Entities;
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
            1,
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
    public void EnsurePublishable_WhenQuestionHasNoTimeLimit_RejectsQuiz()
    {
        var quiz = TriviaQuiz.Create(
            "Intro Quiz",
            "Warm-up trivia",
            [
                TriviaQuestion.Create(
                    "Question 1",
                    1,
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
}
