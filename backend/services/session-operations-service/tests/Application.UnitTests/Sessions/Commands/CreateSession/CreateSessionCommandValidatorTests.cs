using umbral_backend.Application.Sessions.Commands.CreateSession;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.CreateSession;

public sealed class CreateSessionCommandValidatorTests
{
    private readonly CreateSessionCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WhenCommandIsValid_Passes()
    {
        var command = new CreateSessionCommand(
            7,
            "Mission Session",
            15,
            new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero));

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WhenMissionIdIsNotPositive_Fails()
    {
        var result = await _validator.ValidateAsync(new CreateSessionCommand(
            0,
            "Mission Session",
            15,
            DateTimeOffset.UtcNow));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(CreateSessionCommand.MissionId));
    }

    [Fact]
    public async Task Validate_WhenTitleIsEmpty_Fails()
    {
        var result = await _validator.ValidateAsync(new CreateSessionCommand(
            7,
            string.Empty,
            15,
            DateTimeOffset.UtcNow));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(CreateSessionCommand.Title));
    }

    [Fact]
    public async Task Validate_WhenMaximumTimeMinutesIsNotPositive_Fails()
    {
        var result = await _validator.ValidateAsync(new CreateSessionCommand(
            7,
            "Mission Session",
            0,
            DateTimeOffset.UtcNow));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(CreateSessionCommand.MaximumTimeMinutes));
    }

    [Fact]
    public async Task Validate_WhenScheduledAtIsDefault_Fails()
    {
        var result = await _validator.ValidateAsync(new CreateSessionCommand(
            7,
            "Mission Session",
            15,
            default));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(CreateSessionCommand.ScheduledAt));
    }
}
