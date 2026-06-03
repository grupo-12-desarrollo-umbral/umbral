using umbral_backend.Application.Sessions.Commands.CreateTriviaSession;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.CreateTriviaSession;

public sealed class CreateTriviaSessionCommandValidatorTests
{
    private readonly CreateTriviaSessionCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WhenCommandIsValid_Passes()
    {
        var command = new CreateTriviaSessionCommand(
            42,
            "Smoke Trivia",
            15,
            new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero));

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WhenSourceTriviaQuizIdIsNotPositive_Fails()
    {
        var result = await _validator.ValidateAsync(new CreateTriviaSessionCommand(
            0,
            "Smoke Trivia",
            15,
            DateTimeOffset.UtcNow));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(CreateTriviaSessionCommand.SourceTriviaQuizId));
    }

    [Fact]
    public async Task Validate_WhenTitleIsEmpty_Fails()
    {
        var result = await _validator.ValidateAsync(new CreateTriviaSessionCommand(
            42,
            string.Empty,
            15,
            DateTimeOffset.UtcNow));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(CreateTriviaSessionCommand.Title));
    }

    [Fact]
    public async Task Validate_WhenMaximumTimeMinutesIsNotPositive_Fails()
    {
        var result = await _validator.ValidateAsync(new CreateTriviaSessionCommand(
            42,
            "Smoke Trivia",
            0,
            DateTimeOffset.UtcNow));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(CreateTriviaSessionCommand.MaximumTimeMinutes));
    }

    [Fact]
    public async Task Validate_WhenScheduledAtIsDefault_Fails()
    {
        var result = await _validator.ValidateAsync(new CreateTriviaSessionCommand(
            42,
            "Smoke Trivia",
            15,
            default));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(CreateTriviaSessionCommand.ScheduledAt));
    }
}
