using umbral_backend.Application.Missions.Commands.UpdateMission;

namespace umbral_backend.Application.UnitTests.Application.Missions.Commands.UpdateMission;

public sealed class UpdateMissionCommandValidatorTests
{
    private readonly UpdateMissionCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_HasNoErrors()
    {
        var result = _validator.Validate(new UpdateMissionCommand(1, "Mission", "Briefing", "Advanced", 45));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenIdIsNotPositive_ReturnsError()
    {
        var result = _validator.Validate(new UpdateMissionCommand(0, "Mission", "Briefing", "Advanced", 45));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(UpdateMissionCommand.Id));
    }

    [Fact]
    public void Validate_WhenNameIsEmpty_ReturnsError()
    {
        var result = _validator.Validate(new UpdateMissionCommand(1, "", "Briefing", "Advanced", 45));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(UpdateMissionCommand.Name));
    }

    [Fact]
    public void Validate_WhenDescriptionIsEmpty_ReturnsError()
    {
        var result = _validator.Validate(new UpdateMissionCommand(1, "Mission", "", "Advanced", 45));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(UpdateMissionCommand.Description));
    }

    [Fact]
    public void Validate_WhenDifficultyIsEmpty_ReturnsError()
    {
        var result = _validator.Validate(new UpdateMissionCommand(1, "Mission", "Briefing", "", 45));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(UpdateMissionCommand.Difficulty));
    }

    [Fact]
    public void Validate_WhenMaximumTimeIsNotPositive_ReturnsError()
    {
        var result = _validator.Validate(new UpdateMissionCommand(1, "Mission", "Briefing", "Advanced", 0));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(UpdateMissionCommand.MaximumTimeMinutes));
    }
}
