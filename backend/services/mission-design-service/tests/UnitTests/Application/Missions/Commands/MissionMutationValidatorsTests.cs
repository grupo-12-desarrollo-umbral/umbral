using umbral_backend.Application.Missions.Commands.AddTarget;
using umbral_backend.Application.Missions.Commands.RemoveMissionNode;
using umbral_backend.Application.Missions.Commands.RemoveTarget;
using umbral_backend.Application.Missions.Commands.UnassociateClueFromTarget;
using umbral_backend.Application.Missions.Commands.UpdateMissionNode;
using umbral_backend.Application.Missions.Commands.UpdateTarget;
using umbral_backend.Application.Missions.Commands.SelectTriviaQuiz;

namespace umbral_backend.Application.UnitTests.Application.Missions.Commands;

public sealed class MissionMutationValidatorsTests
{
    // ── RemoveMissionNode ─────────────────────────────────────────────────────

    [Fact]
    public void RemoveMissionNode_ValidCommand_PassesValidation()
    {
        var result = new RemoveMissionNodeCommandValidator()
            .Validate(new RemoveMissionNodeCommand(1, 2));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RemoveMissionNode_InvalidIds_FailsValidation()
    {
        var result = new RemoveMissionNodeCommandValidator()
            .Validate(new RemoveMissionNodeCommand(0, 0));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(RemoveMissionNodeCommand.MissionId));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RemoveMissionNodeCommand.NodeId));
    }

    // ── RemoveTarget ──────────────────────────────────────────────────────────

    [Fact]
    public void RemoveTarget_ValidCommand_PassesValidation()
    {
        var result = new RemoveTargetCommandValidator()
            .Validate(new RemoveTargetCommand(1, 2, 3, 4));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RemoveTarget_ZeroIds_FailsValidation()
    {
        var result = new RemoveTargetCommandValidator()
            .Validate(new RemoveTargetCommand(0, 0, 0, 0));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(RemoveTargetCommand.MissionId));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RemoveTargetCommand.StageId));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RemoveTargetCommand.SubstageId));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RemoveTargetCommand.TargetId));
    }

    // ── UpdateMissionNode ─────────────────────────────────────────────────────

    [Fact]
    public void UpdateMissionNode_ValidCommand_PassesValidation()
    {
        var result = new UpdateMissionNodeCommandValidator()
            .Validate(new UpdateMissionNodeCommand(1, 2, "Title", 1));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateMissionNode_EmptyTitle_FailsValidation()
    {
        var result = new UpdateMissionNodeCommandValidator()
            .Validate(new UpdateMissionNodeCommand(1, 2, "", 1));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateMissionNodeCommand.Title));
    }

    [Fact]
    public void UpdateMissionNode_InvalidClueVisibilityPolicy_FailsValidation()
    {
        var result = new UpdateMissionNodeCommandValidator()
            .Validate(new UpdateMissionNodeCommand(1, 2, "Title", 1, ClueVisibilityPolicy: "Unknown"));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateMissionNodeCommand.ClueVisibilityPolicy));
    }

    [Fact]
    public void UpdateMissionNode_ValidClueVisibilityPolicy_PassesValidation()
    {
        var result = new UpdateMissionNodeCommandValidator()
            .Validate(new UpdateMissionNodeCommand(1, 2, "Title", 1, ClueVisibilityPolicy: "VisibleWhenSubstageStarts"));

        result.IsValid.Should().BeTrue();
    }

    // ── UpdateTarget ──────────────────────────────────────────────────────────

    [Fact]
    public void UpdateTarget_ValidCommand_PassesValidation()
    {
        var result = new UpdateTargetCommandValidator()
            .Validate(new UpdateTargetCommand(1, 2, 3, 4, "Name", "QR", 1, 4.711, -74.0721, true));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateTarget_EmptyNameAndQrCode_FailsValidation()
    {
        var result = new UpdateTargetCommandValidator()
            .Validate(new UpdateTargetCommand(1, 2, 3, 4, "", "", 1, 4.711, -74.0721, true));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateTargetCommand.Name));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateTargetCommand.QrCode));
    }

    // ── AddTarget ─────────────────────────────────────────────────────────────

    // Score is not a command field: it is derived from the mission's difficulty,
    // so the validators no longer carry any score rule.
    [Fact]
    public void AddTarget_ValidCommand_PassesValidation()
    {
        var result = new AddTargetCommandValidator()
            .Validate(new AddTargetCommand(1, 2, 3, "Name", "QR", 1, 4.711, -74.0721));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-90.1, 0)]
    [InlineData(90.1, 0)]
    [InlineData(0, -180.1)]
    [InlineData(0, 180.1)]
    public void AddTarget_CoordinatesOutOfRange_FailsValidation(double latitude, double longitude)
    {
        var result = new AddTargetCommandValidator()
            .Validate(new AddTargetCommand(1, 2, 3, "Name", "QR", 1, latitude, longitude));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(AddTargetCommand.Latitude) ||
            e.PropertyName == nameof(AddTargetCommand.Longitude));
    }

    [Theory]
    [InlineData(-90.1, 0)]
    [InlineData(0, 180.1)]
    public void UpdateTarget_CoordinatesOutOfRange_FailsValidation(double latitude, double longitude)
    {
        var result = new UpdateTargetCommandValidator()
            .Validate(new UpdateTargetCommand(1, 2, 3, 4, "Name", "QR", 1, latitude, longitude, true));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateTargetCommand.Latitude) ||
            e.PropertyName == nameof(UpdateTargetCommand.Longitude));
    }

    // ── UpdateTriviaQuizSelection ─────────────────────────────────────────────

    [Fact]
    public void UpdateTriviaQuizSelection_ValidCommand_PassesValidation()
    {
        var result = new SelectTriviaQuizCommandValidator()
            .Validate(new SelectTriviaQuizCommand(1, 2, 3, 4));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateTriviaQuizSelection_ZeroIds_FailsValidation()
    {
        var result = new SelectTriviaQuizCommandValidator()
            .Validate(new SelectTriviaQuizCommand(0, 0, 0, 0));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(SelectTriviaQuizCommand.MissionId));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SelectTriviaQuizCommand.TriviaQuizId));
    }

    // ── UnassociateClueFromTarget ─────────────────────────────────────────────

    [Fact]
    public void UnassociateClueFromTarget_ValidCommand_PassesValidation()
    {
        var result = new UnassociateClueFromTargetCommandValidator()
            .Validate(new UnassociateClueFromTargetCommand(1, 2, 3, 4));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UnassociateClueFromTarget_ZeroIds_FailsValidation()
    {
        var result = new UnassociateClueFromTargetCommandValidator()
            .Validate(new UnassociateClueFromTargetCommand(0, 0, 0, 0));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(UnassociateClueFromTargetCommand.MissionId));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UnassociateClueFromTargetCommand.TargetId));
    }
}
