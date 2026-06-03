using umbral_backend.Application.Trivias.Commands.DuplicateTriviaQuiz;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Commands.DuplicateTriviaQuiz;

public sealed class DuplicateTriviaQuizCommandValidatorTests
{
    private readonly DuplicateTriviaQuizCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_HasNoErrors()
    {
        var result = _validator.Validate(new DuplicateTriviaQuizCommand(1));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenIdIsNotPositive_ReturnsError()
    {
        var result = _validator.Validate(new DuplicateTriviaQuizCommand(0));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(DuplicateTriviaQuizCommand.Id));
    }
}
