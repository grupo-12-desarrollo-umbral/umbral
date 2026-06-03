using umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.ReconnectAuthenticatedParticipant;

public sealed class ReconnectAuthenticatedParticipantCommandValidatorTests
{
    private readonly ReconnectAuthenticatedParticipantCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WhenCommandIsValid_Passes()
    {
        var command = new ReconnectAuthenticatedParticipantCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Nora",
            4,
            "join-token");

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WhenLiveSessionIdIsEmpty_Fails()
    {
        var result = await _validator.ValidateAsync(new ReconnectAuthenticatedParticipantCommand(
            Guid.Empty,
            Guid.NewGuid(),
            "Nora",
            4,
            null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(ReconnectAuthenticatedParticipantCommand.LiveSessionId));
    }

    [Fact]
    public async Task Validate_WhenTeamIdIsEmpty_Fails()
    {
        var result = await _validator.ValidateAsync(new ReconnectAuthenticatedParticipantCommand(
            Guid.NewGuid(),
            Guid.Empty,
            "Nora",
            4,
            null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(ReconnectAuthenticatedParticipantCommand.TeamId));
    }

    [Fact]
    public async Task Validate_WhenDisplayNameIsEmpty_Fails()
    {
        var result = await _validator.ValidateAsync(new ReconnectAuthenticatedParticipantCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            string.Empty,
            4,
            null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(ReconnectAuthenticatedParticipantCommand.DisplayName));
    }

    [Fact]
    public async Task Validate_WhenTeamCapacityIsNotPositive_Fails()
    {
        var result = await _validator.ValidateAsync(new ReconnectAuthenticatedParticipantCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Nora",
            0,
            null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(ReconnectAuthenticatedParticipantCommand.TeamCapacity));
    }
}
