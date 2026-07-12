using umbral_backend.Application.Trivias.Commands.CreateTriviaQuiz;
using umbral_backend.Application.Trivias.Common.Authoring;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Commands.CreateTriviaQuiz;

public sealed class CreateTriviaQuizCommandValidatorTests
{
    private readonly CreateTriviaQuizCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_HasNoErrors()
    {
        var result = _validator.Validate(new CreateTriviaQuizCommand(
            "Quiz",
            "Warm-up trivia",
            [
                new TriviaQuestionInput(
                    "Question?",
                    true,
                    [
                        new TriviaOptionInput("Correct", 1, true),
                        new TriviaOptionInput("Incorrect", 2, false)
                    ])
            ]));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenTitleIsEmpty_ReturnsError()
    {
        var result = _validator.Validate(new CreateTriviaQuizCommand("", "Warm-up trivia", []));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(CreateTriviaQuizCommand.Title));
    }

    [Fact]
    public void Validate_WhenDescriptionIsEmpty_ReturnsError()
    {
        var result = _validator.Validate(new CreateTriviaQuizCommand("Quiz", "", []));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(CreateTriviaQuizCommand.Description));
    }

    [Fact]
    public void Validate_WhenQuestionsShareLegacySequenceOrder_DoesNotReturnQuestionCollectionError()
    {
        var result = _validator.Validate(new CreateTriviaQuizCommand(
            "Quiz",
            "Warm-up trivia",
            [
                new TriviaQuestionInput("Question 1?", true, []),
                new TriviaQuestionInput("Question 2?", true, [])
            ]));

        result.Errors.Should().NotContain(error => error.PropertyName == nameof(CreateTriviaQuizCommand.Questions));
    }

    [Fact]
    public void Validate_WhenOptionSequenceOrderIsDuplicated_ReturnsError()
    {
        var result = _validator.Validate(new CreateTriviaQuizCommand(
            "Quiz",
            "Warm-up trivia",
            [
                new TriviaQuestionInput(
                    "Question?",
                    true,
                    [
                        new TriviaOptionInput("A", 1, true),
                        new TriviaOptionInput("B", 1, false)
                    ])
            ]));

        result.Errors.Should().ContainSingle(error => error.PropertyName == "Questions[0].Options");
    }

    [Fact]
    public void Validate_WhenMoreThanOneOptionIsMarkedCorrect_ReturnsError()
    {
        var result = _validator.Validate(new CreateTriviaQuizCommand(
            "Quiz",
            "Warm-up trivia",
            [
                new TriviaQuestionInput(
                    "Question?",
                    true,
                    [
                        new TriviaOptionInput("A", 1, true),
                        new TriviaOptionInput("B", 2, true)
                    ])
            ]));

        result.Errors.Should().ContainSingle(error => error.PropertyName == "Questions[0].Options");
    }
}
