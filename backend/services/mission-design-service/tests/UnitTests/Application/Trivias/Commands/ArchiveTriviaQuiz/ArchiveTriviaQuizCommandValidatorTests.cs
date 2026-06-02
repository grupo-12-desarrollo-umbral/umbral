using umbral_backend.Application.Trivias.Commands.ArchiveTriviaQuiz;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Commands.ArchiveTriviaQuiz;

public sealed class ArchiveTriviaQuizCommandValidatorTests
{
    private readonly ArchiveTriviaQuizCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_HasNoErrors()
    {
        var result = _validator.Validate(new ArchiveTriviaQuizCommand(1));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenIdIsNotPositive_ReturnsError()
    {
        var result = _validator.Validate(new ArchiveTriviaQuizCommand(0));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(ArchiveTriviaQuizCommand.Id));
    }
}
