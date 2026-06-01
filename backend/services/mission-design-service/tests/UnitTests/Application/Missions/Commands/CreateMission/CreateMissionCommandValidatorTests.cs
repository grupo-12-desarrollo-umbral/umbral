using umbral_backend.Application.Missions.Commands.CreateMission;

namespace umbral_backend.Application.UnitTests.Application.Missions.Commands.CreateMission;

public sealed class CreateMissionCommandValidatorTests
{
    private readonly CreateMissionCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_HasNoErrors()
    {
        var result = _validator.Validate(new CreateMissionCommand("Mission", "Briefing", "Advanced", 45));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenNameIsEmpty_ReturnsError()
    {
        var result = _validator.Validate(new CreateMissionCommand("", "Briefing", "Advanced", 45));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(CreateMissionCommand.Name));
    }

    [Fact]
    public void Validate_WhenDescriptionIsEmpty_ReturnsError()
    {
        var result = _validator.Validate(new CreateMissionCommand("Mission", "", "Advanced", 45));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(CreateMissionCommand.Description));
    }

    [Fact]
    public void Validate_WhenDifficultyIsEmpty_ReturnsError()
    {
        var result = _validator.Validate(new CreateMissionCommand("Mission", "Briefing", "", 45));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(CreateMissionCommand.Difficulty));
    }

    [Fact]
    public void Validate_WhenMaximumTimeIsNotPositive_ReturnsError()
    {
        var result = _validator.Validate(new CreateMissionCommand("Mission", "Briefing", "Advanced", 0));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(CreateMissionCommand.MaximumTimeMinutes));
    }
}
