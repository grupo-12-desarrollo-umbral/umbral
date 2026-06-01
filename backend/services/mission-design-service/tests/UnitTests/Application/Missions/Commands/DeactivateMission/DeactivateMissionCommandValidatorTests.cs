using umbral_backend.Application.Missions.Commands.DeactivateMission;

namespace umbral_backend.Application.UnitTests.Application.Missions.Commands.DeactivateMission;

public sealed class DeactivateMissionCommandValidatorTests
{
    private readonly DeactivateMissionCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_HasNoErrors()
    {
        var result = _validator.Validate(new DeactivateMissionCommand(1));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenIdIsNotPositive_ReturnsError()
    {
        var result = _validator.Validate(new DeactivateMissionCommand(0));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(DeactivateMissionCommand.Id));
    }
}
