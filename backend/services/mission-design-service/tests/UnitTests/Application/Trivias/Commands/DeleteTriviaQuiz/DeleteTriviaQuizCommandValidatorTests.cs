using umbral_backend.Application.Trivias.Commands.DeleteTriviaQuiz;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Commands.DeleteTriviaQuiz;

public sealed class DeleteTriviaQuizCommandValidatorTests
{
    private readonly DeleteTriviaQuizCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_HasNoErrors()
    {
        var result = _validator.Validate(new DeleteTriviaQuizCommand(1));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenIdIsNotPositive_ReturnsError()
    {
        var result = _validator.Validate(new DeleteTriviaQuizCommand(0));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(DeleteTriviaQuizCommand.Id));
    }
}
