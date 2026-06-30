using umbral_backend.Application.Missions.Commands.ActivateMission;
using umbral_backend.Application.Missions.Commands.AddMissionNode;
using umbral_backend.Application.Missions.Commands.AddTarget;
using umbral_backend.Application.Missions.Commands.AssignSubstagePlayMode;
using umbral_backend.Application.Missions.Commands.AssociateClueWithTarget;
using umbral_backend.Application.Missions.Commands.SelectTriviaQuiz;

namespace umbral_backend.Application.UnitTests.Application.Missions.Commands;

public sealed class MissionRuntimePlanCommandValidatorTests
{
    [Fact]
    public void AddMissionNode_ValidatesHierarchySpecificFields()
    {
        var validator = new AddMissionNodeCommandValidator();

        var result = validator.Validate(new AddMissionNodeCommand(1, "Substage", "Substage", 1, PlayMode: "TreasureHunt"));

        result.Errors.Should().Contain(error => error.PropertyName == nameof(AddMissionNodeCommand.StageId));
    }

    [Fact]
    public void AddMissionNode_RejectsUnknownNodeType()
    {
        var validator = new AddMissionNodeCommandValidator();

        var result = validator.Validate(new AddMissionNodeCommand(1, "RuntimeStep", "Node", 1));

        result.Errors.Should().Contain(error => error.PropertyName == nameof(AddMissionNodeCommand.NodeType));
    }

    [Fact]
    public void AssignSubstagePlayMode_RejectsUnknownMode()
    {
        var validator = new AssignSubstagePlayModeCommandValidator();

        var result = validator.Validate(new AssignSubstagePlayModeCommand(1, 2, 3, "Mixed"));

        result.Errors.Should().Contain(error => error.PropertyName == nameof(AssignSubstagePlayModeCommand.PlayMode));
    }

    [Fact]
    public void AddTarget_RequiresTargetOwnershipPath()
    {
        var validator = new AddTargetCommandValidator();

        var result = validator.Validate(new AddTargetCommand(1, 0, 0, "Target", "QR", 1));

        result.Errors.Should().Contain(error => error.PropertyName == nameof(AddTargetCommand.StageId));
        result.Errors.Should().Contain(error => error.PropertyName == nameof(AddTargetCommand.SubstageId));
    }

    [Fact]
    public void AssociateClueWithTarget_RequiresTargetAndClueIds()
    {
        var validator = new AssociateClueWithTargetCommandValidator();

        var result = validator.Validate(new AssociateClueWithTargetCommand(1, 2, 3, 0, 0));

        result.Errors.Should().Contain(error => error.PropertyName == nameof(AssociateClueWithTargetCommand.TargetId));
        result.Errors.Should().Contain(error => error.PropertyName == nameof(AssociateClueWithTargetCommand.ClueId));
    }

    [Fact]
    public void SetTriviaQuizSelection_AcceptsWholeQuizReference()
    {
        var validator = new SelectTriviaQuizCommandValidator();

        var result = validator.Validate(new SelectTriviaQuizCommand(1, 2, 3, 4));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SetTriviaQuizSelection_RejectsNonPositiveTriviaQuizId()
    {
        var validator = new SelectTriviaQuizCommandValidator();

        var result = validator.Validate(new SelectTriviaQuizCommand(1, 2, 3, 0));

        result.Errors.Should().Contain(error => error.PropertyName == nameof(SelectTriviaQuizCommand.TriviaQuizId));
    }

    [Fact]
    public void ActivateMission_RequiresMissionId()
    {
        var validator = new ActivateMissionCommandValidator();

        var result = validator.Validate(new ActivateMissionCommand(0));

        result.Errors.Should().Contain(error => error.PropertyName == nameof(ActivateMissionCommand.Id));
    }
}
