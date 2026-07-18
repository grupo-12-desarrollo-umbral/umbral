using umbral_backend.Application.Teams.Commands.UpdateTeam;

namespace umbral_backend.Application.UnitTests.Application.Teams.Commands.UpdateTeam;

public sealed class UpdateTeamCommandValidatorTests
{
    private readonly UpdateTeamCommandValidator _validator = new();

    [Fact]
    public void Validate_AcceptsValidTeamPayload()
    {
        var result = _validator.Validate(new UpdateTeamCommand(Guid.NewGuid(), "Blue Team", "BLUE-01"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsEmptyIdentifierAndFields()
    {
        var result = _validator.Validate(new UpdateTeamCommand(Guid.Empty, string.Empty, string.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain(new[]
        {
            nameof(UpdateTeamCommand.TeamId),
            nameof(UpdateTeamCommand.DisplayName),
            nameof(UpdateTeamCommand.TeamCode)
        });
    }
}
