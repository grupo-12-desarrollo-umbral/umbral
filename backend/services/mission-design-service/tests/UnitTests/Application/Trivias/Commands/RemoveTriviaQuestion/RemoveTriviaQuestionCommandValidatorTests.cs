using umbral_backend.Application.Trivias.Commands.RemoveTriviaQuestion;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Commands.RemoveTriviaQuestion;

public sealed class RemoveTriviaQuestionCommandValidatorTests
{
    private readonly RemoveTriviaQuestionCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_HasNoErrors()
    {
        var result = _validator.Validate(new RemoveTriviaQuestionCommand(1, 7));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenTriviaQuizIdIsNotPositive_ReturnsError()
    {
        var result = _validator.Validate(new RemoveTriviaQuestionCommand(0, 7));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(RemoveTriviaQuestionCommand.TriviaQuizId));
    }

    [Fact]
    public void Validate_WhenQuestionIdIsNotPositive_ReturnsError()
    {
        var result = _validator.Validate(new RemoveTriviaQuestionCommand(1, 0));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(RemoveTriviaQuestionCommand.QuestionId));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    [InlineData(0, 0)]
    public void Validate_WhenIdsAreInvalid_ReturnsErrors(int triviaQuizId, int questionId)
    {
        var result = _validator.Validate(new RemoveTriviaQuestionCommand(triviaQuizId, questionId));

        result.IsValid.Should().BeFalse();
    }
}
