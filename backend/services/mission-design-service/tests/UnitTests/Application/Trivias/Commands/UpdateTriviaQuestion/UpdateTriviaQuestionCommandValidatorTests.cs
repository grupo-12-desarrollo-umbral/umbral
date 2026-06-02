using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuestion;
using umbral_backend.Application.Trivias.Common.Authoring;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Commands.UpdateTriviaQuestion;

public sealed class UpdateTriviaQuestionCommandValidatorTests
{
    private readonly UpdateTriviaQuestionCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_HasNoErrors()
    {
        var result = _validator.Validate(new UpdateTriviaQuestionCommand(
            1,
            7,
            "Question?",
            1,
            100,
            30,
            null,
            true,
            [
                new TriviaOptionInput("Correct", 1, true),
                new TriviaOptionInput("Incorrect", 2, false)
            ]));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenQuestionIdIsNotPositive_ReturnsError()
    {
        var result = _validator.Validate(new UpdateTriviaQuestionCommand(
            1,
            0,
            "Question?",
            1,
            100,
            30,
            null,
            true,
            [
                new TriviaOptionInput("Correct", 1, true),
                new TriviaOptionInput("Incorrect", 2, false)
            ]));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(UpdateTriviaQuestionCommand.QuestionId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_WhenScoreValueIsOutsideRange_ReturnsError(int scoreValue)
    {
        var result = _validator.Validate(new UpdateTriviaQuestionCommand(
            1,
            7,
            "Question?",
            1,
            scoreValue,
            30,
            null,
            true,
            [
                new TriviaOptionInput("Correct", 1, true),
                new TriviaOptionInput("Incorrect", 2, false)
            ]));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(UpdateTriviaQuestionCommand.ScoreValue));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(121)]
    public void Validate_WhenTimeLimitIsOutsideRange_ReturnsError(int timeLimitSeconds)
    {
        var result = _validator.Validate(new UpdateTriviaQuestionCommand(
            1,
            7,
            "Question?",
            1,
            100,
            timeLimitSeconds,
            null,
            true,
            [
                new TriviaOptionInput("Correct", 1, true),
                new TriviaOptionInput("Incorrect", 2, false)
            ]));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(UpdateTriviaQuestionCommand.TimeLimitSeconds));
    }

    [Fact]
    public void Validate_WhenOptionSequenceOrderIsDuplicated_ReturnsError()
    {
        var result = _validator.Validate(new UpdateTriviaQuestionCommand(
            1,
            7,
            "Question?",
            1,
            100,
            30,
            null,
            true,
            [
                new TriviaOptionInput("A", 1, true),
                new TriviaOptionInput("B", 1, false)
            ]));

        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpdateTriviaQuestionCommand.Options));
    }
}
