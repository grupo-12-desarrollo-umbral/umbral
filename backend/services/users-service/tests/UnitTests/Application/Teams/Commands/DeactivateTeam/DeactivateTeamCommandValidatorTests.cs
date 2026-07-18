using umbral_backend.Application.Teams.Commands.DeactivateTeam;

namespace umbral_backend.Application.UnitTests.Application.Teams.Commands.DeactivateTeam;

public sealed class DeactivateTeamCommandValidatorTests
{
    private readonly DeactivateTeamCommandValidator _validator = new();

    [Fact]
    public void Validate_AcceptsNonEmptyIdentifier()
    {
        var result = _validator.Validate(new DeactivateTeamCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsEmptyIdentifier()
    {
        var result = _validator.Validate(new DeactivateTeamCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(DeactivateTeamCommand.TeamId));
    }
}
