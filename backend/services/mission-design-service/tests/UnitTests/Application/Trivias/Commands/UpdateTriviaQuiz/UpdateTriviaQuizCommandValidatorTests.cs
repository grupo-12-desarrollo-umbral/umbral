using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuiz;
using umbral_backend.Application.Trivias.Common.Authoring;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Commands.UpdateTriviaQuiz;

public sealed class UpdateTriviaQuizCommandValidatorTests
{
    private readonly UpdateTriviaQuizCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_HasNoErrors()
    {
        var result = _validator.Validate(new UpdateTriviaQuizCommand(
            1,
            "Quiz",
            "Warm-up trivia",
            [
                new TriviaQuestionInput(
                    "Question?",
                    1,
                    true,
                    [
                        new TriviaOptionInput("Correct", 1, true),
                        new TriviaOptionInput("Incorrect", 2, false)
                    ])
            ]));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenIdIsNotPositive_ReturnsError()
    {
        var result = _validator.Validate(new UpdateTriviaQuizCommand(0, "Quiz", "Warm-up trivia", []));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(UpdateTriviaQuizCommand.Id));
    }

    [Fact]
    public void Validate_WhenQuestionPromptIsEmpty_ReturnsError()
    {
        var result = _validator.Validate(new UpdateTriviaQuizCommand(
            1,
            "Quiz",
            "Warm-up trivia",
            [
                new TriviaQuestionInput("", 1, true, [])
            ]));

        result.Errors.Should().ContainSingle(error => error.PropertyName == "Questions[0].Prompt");
    }
}
