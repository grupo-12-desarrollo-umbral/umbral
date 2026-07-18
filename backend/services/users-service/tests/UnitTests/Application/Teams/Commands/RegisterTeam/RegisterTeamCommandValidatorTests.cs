using umbral_backend.Application.Teams.Commands.RegisterTeam;

namespace umbral_backend.Application.UnitTests.Application.Teams.Commands.RegisterTeam;

public sealed class RegisterTeamCommandValidatorTests
{
    private readonly RegisterTeamCommandValidator _validator = new();

    [Fact]
    public void Validate_AcceptsRequiredFields()
    {
        var result = _validator.Validate(new RegisterTeamCommand("Red Team", "RED-01"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsMissingRequiredFields()
    {
        var result = _validator.Validate(new RegisterTeamCommand(string.Empty, string.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain(new[]
        {
            nameof(RegisterTeamCommand.DisplayName),
            nameof(RegisterTeamCommand.TeamCode)
        });
    }
}
