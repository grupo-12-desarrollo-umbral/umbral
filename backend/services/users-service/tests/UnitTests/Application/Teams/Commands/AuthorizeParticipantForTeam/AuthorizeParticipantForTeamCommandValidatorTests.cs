using umbral_backend.Application.Teams.Commands.AuthorizeParticipantForTeam;

namespace umbral_backend.Application.UnitTests.Application.Teams.Commands.AuthorizeParticipantForTeam;

public sealed class AuthorizeParticipantForTeamCommandValidatorTests
{
    private readonly AuthorizeParticipantForTeamCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidValues_Succeeds()
    {
        var result = await _validator.ValidateAsync(new AuthorizeParticipantForTeamCommand(Guid.NewGuid(), 42));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithInvalidValues_Fails()
    {
        var result = await _validator.ValidateAsync(new AuthorizeParticipantForTeamCommand(Guid.Empty, 0));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().BeEquivalentTo(
            nameof(AuthorizeParticipantForTeamCommand.TeamId),
            nameof(AuthorizeParticipantForTeamCommand.UserId));
    }
}
