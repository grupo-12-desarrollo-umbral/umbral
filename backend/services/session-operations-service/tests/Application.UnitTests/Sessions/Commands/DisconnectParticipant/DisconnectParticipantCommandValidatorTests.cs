using umbral_backend.Application.Sessions.Commands.DisconnectParticipant;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.DisconnectParticipant;

public sealed class DisconnectParticipantCommandValidatorTests
{
    private readonly DisconnectParticipantCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WhenCommandIsWellFormed_Succeeds()
    {
        var command = new DisconnectParticipantCommand(Guid.NewGuid(), Guid.NewGuid(), "conn-1");

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WhenLiveSessionIdIsEmpty_Fails()
    {
        var result = await _validator.ValidateAsync(new DisconnectParticipantCommand(Guid.Empty, Guid.NewGuid(), "conn-1"));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(DisconnectParticipantCommand.LiveSessionId));
    }

    [Fact]
    public async Task Validate_WhenSessionParticipantIdIsEmpty_Fails()
    {
        var result = await _validator.ValidateAsync(new DisconnectParticipantCommand(Guid.NewGuid(), Guid.Empty, "conn-1"));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(DisconnectParticipantCommand.SessionParticipantId));
    }

    [Fact]
    public async Task Validate_WhenConnectionIdIsEmpty_Fails()
    {
        var result = await _validator.ValidateAsync(new DisconnectParticipantCommand(Guid.NewGuid(), Guid.NewGuid(), string.Empty));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(DisconnectParticipantCommand.ConnectionId));
    }
}
