using umbral_backend.Application.Trivias.Commands.RetireTriviaQuiz;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Commands.RetireTriviaQuiz;

public sealed class RetireTriviaQuizCommandValidatorTests
{
    private readonly RetireTriviaQuizCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_HasNoErrors()
    {
        var result = _validator.Validate(new RetireTriviaQuizCommand(1));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenIdIsNotPositive_ReturnsError()
    {
        var result = _validator.Validate(new RetireTriviaQuizCommand(0));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(RetireTriviaQuizCommand.Id));
    }
}
