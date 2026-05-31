using umbral_backend.Application.Teams.Commands.AssignParticipantToTeam;

namespace umbral_backend.Application.UnitTests.Application.Teams.Commands.AssignParticipantToTeam;

public sealed class AssignParticipantToTeamCommandValidatorTests
{
    private readonly AssignParticipantToTeamCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidValues_Succeeds()
    {
        var result = await _validator.ValidateAsync(new AssignParticipantToTeamCommand(Guid.NewGuid(), 42));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithInvalidValues_Fails()
    {
        var result = await _validator.ValidateAsync(new AssignParticipantToTeamCommand(Guid.Empty, 0));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().BeEquivalentTo(
            nameof(AssignParticipantToTeamCommand.TeamId),
            nameof(AssignParticipantToTeamCommand.UserId));
    }
}
