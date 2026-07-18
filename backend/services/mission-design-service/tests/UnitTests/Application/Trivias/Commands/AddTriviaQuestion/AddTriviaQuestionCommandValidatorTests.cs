using umbral_backend.Application.Trivias.Commands.AddTriviaQuestion;
using umbral_backend.Application.Trivias.Common.Authoring;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Commands.AddTriviaQuestion;

public sealed class AddTriviaQuestionCommandValidatorTests
{
    private readonly AddTriviaQuestionCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_HasNoErrors()
    {
        var result = _validator.Validate(new AddTriviaQuestionCommand(
            1,
            "Question?",
            100,
            30,
            "Optional explanation",
            true,
            [
                new TriviaOptionInput("Correct", 1, true),
                new TriviaOptionInput("Incorrect", 2, false)
            ]));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenTriviaQuizIdIsNotPositive_ReturnsError()
    {
        var result = _validator.Validate(new AddTriviaQuestionCommand(
            0,
            "Question?",
            100,
            30,
            null,
            true,
            [
                new TriviaOptionInput("Correct", 1, true),
                new TriviaOptionInput("Incorrect", 2, false)
            ]));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(AddTriviaQuestionCommand.TriviaQuizId));
    }

    [Fact]
    public void Validate_WhenOnlyOneOptionIsProvided_ReturnsError()
    {
        var result = _validator.Validate(new AddTriviaQuestionCommand(
            1,
            "Question?",
            100,
            30,
            null,
            true,
            [
                new TriviaOptionInput("Only option", 1, true)
            ]));

        result.Errors.Should().Contain(error => error.PropertyName == nameof(AddTriviaQuestionCommand.Options));
    }

    [Fact]
    public void Validate_WhenNoOptionIsMarkedCorrect_ReturnsError()
    {
        var result = _validator.Validate(new AddTriviaQuestionCommand(
            1,
            "Question?",
            100,
            30,
            null,
            true,
            [
                new TriviaOptionInput("A", 1, false),
                new TriviaOptionInput("B", 2, false)
            ]));

        result.Errors.Should().Contain(error => error.PropertyName == nameof(AddTriviaQuestionCommand.Options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(99)]
    [InlineData(101)]
    public void Validate_WhenScoreValueIsNotExactlyOneHundred_ReturnsError(int scoreValue)
    {
        var result = _validator.Validate(new AddTriviaQuestionCommand(
            1,
            "Question?",
            scoreValue,
            30,
            null,
            true,
            [
                new TriviaOptionInput("Correct", 1, true),
                new TriviaOptionInput("Incorrect", 2, false)
            ]));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(AddTriviaQuestionCommand.ScoreValue));
    }

    [Theory]
    [InlineData(14)]
    [InlineData(31)]
    public void Validate_WhenTimeLimitIsOutsideRange_ReturnsError(int timeLimitSeconds)
    {
        var result = _validator.Validate(new AddTriviaQuestionCommand(
            1,
            "Question?",
            100,
            timeLimitSeconds,
            null,
            true,
            [
                new TriviaOptionInput("Correct", 1, true),
                new TriviaOptionInput("Incorrect", 2, false)
            ]));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(AddTriviaQuestionCommand.TimeLimitSeconds));
    }
}
