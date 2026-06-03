using umbral_backend.Application.Teams.Commands.JoinTeamAsParticipant;

namespace umbral_backend.Application.UnitTests.Application.Teams.Commands.JoinTeamAsParticipant;

public sealed class JoinTeamAsParticipantCommandValidatorTests
{
    private readonly JoinTeamAsParticipantCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidInput_Succeeds()
    {
        var result = await _validator.ValidateAsync(
            new JoinTeamAsParticipantCommand(Guid.NewGuid(), Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithEmptyIdentifiers_Fails()
    {
        var result = await _validator.ValidateAsync(
            new JoinTeamAsParticipantCommand(Guid.Empty, Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().BeEquivalentTo(
            nameof(JoinTeamAsParticipantCommand.LiveSessionId),
            nameof(JoinTeamAsParticipantCommand.TeamId));
    }
}
