using umbral_backend.Application.Trivias.Commands.PublishTriviaQuiz;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Commands.PublishTriviaQuiz;

public sealed class PublishTriviaQuizCommandValidatorTests
{
    private readonly PublishTriviaQuizCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_HasNoErrors()
    {
        var result = _validator.Validate(new PublishTriviaQuizCommand(1));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenIdIsNotPositive_ReturnsError()
    {
        var result = _validator.Validate(new PublishTriviaQuizCommand(0));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(PublishTriviaQuizCommand.Id));
    }
}
