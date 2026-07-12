using umbral_backend.Application.Missions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using ApplicationNotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using ApplicationValidationException = umbral_backend.Application.Common.Exceptions.ValidationException;

namespace umbral_backend.Application.UnitTests.Application.Missions.Common;

// Direct coverage of the internal MissionStructureEditor: the command handlers exercise its happy
// paths, but its find-at-each-level fallbacks, the rename/remove clue paths, the play-mode swap
// ternary and the clue-associated-with-target guard are only fully driven here.
public sealed class MissionStructureEditorTests
{
    private const int StageId = 10;
    private const int SubstageId = 20;
    private const int ClueId = 30;
    private const int TargetId = 40;

    private static Mission BuildMission(out Stage stage, out Substage substage)
    {
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        stage = mission.AddStage("Stage", 1);
        stage.Id = StageId;
        substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Substage", 1));
        substage.Id = SubstageId;
        return mission;
    }

    private static Clue AddClue(Mission mission)
    {
        var clue = mission.AddClue(StageId, SubstageId, Clue.Create("Clue", 1, "Find it", ClueVisibilityPolicy.HiddenUntilOperatorRelease));
        clue.Id = ClueId;
        return clue;
    }

    private static Target AddTarget(Substage substage)
    {
        // Advanced mission ⇒ Difficulty.ScoreFactor 3 ⇒ derived target score 50 × 3 = 150.
        var target = substage.AddTarget("Target", "QR-1", 1, 150, 4.711, -74.0721);
        target.Id = TargetId;
        return target;
    }

    // ── Find* ────────────────────────────────────────────────────────────────
    [Fact]
    public void FindStage_Missing_ThrowsNotFound()
    {
        var mission = BuildMission(out _, out _);
        var act = () => MissionStructureEditor.FindStage(mission, 999);
        act.Should().Throw<ApplicationNotFoundException>();
    }

    [Fact]
    public void FindSubstage_Missing_ThrowsNotFound()
    {
        var mission = BuildMission(out _, out _);
        var act = () => MissionStructureEditor.FindSubstage(mission, StageId, 999);
        act.Should().Throw<ApplicationNotFoundException>();
    }

    [Fact]
    public void FindClue_FoundAndMissing()
    {
        var mission = BuildMission(out _, out _);
        AddClue(mission);

        MissionStructureEditor.FindClue(mission, StageId, SubstageId, ClueId).Id.Should().Be(ClueId);
        var act = () => MissionStructureEditor.FindClue(mission, StageId, SubstageId, 999);
        act.Should().Throw<ApplicationNotFoundException>();
    }

    [Fact]
    public void FindTarget_FoundAndMissing()
    {
        var mission = BuildMission(out _, out var substage);
        AddTarget(substage);

        MissionStructureEditor.FindTarget(mission, StageId, SubstageId, TargetId).Id.Should().Be(TargetId);
        var act = () => MissionStructureEditor.FindTarget(mission, StageId, SubstageId, 999);
        act.Should().Throw<ApplicationNotFoundException>();
    }

    // ── RenameNode ───────────────────────────────────────────────────────────
    [Fact]
    public void RenameNode_RenamesStage()
    {
        var mission = BuildMission(out var stage, out _);
        MissionStructureEditor.RenameNode(mission, stage.Id, "Renamed Stage", 2, null, null);
        stage.Title.Should().Be("Renamed Stage");
    }

    [Fact]
    public void RenameNode_RenamesSubstage()
    {
        var mission = BuildMission(out _, out var substage);
        MissionStructureEditor.RenameNode(mission, substage.Id, "Renamed Substage", 3, null, null);
        substage.Title.Should().Be("Renamed Substage");
    }

    [Fact]
    public void RenameNode_ReplacesClue_PreservingIdAndDefaults()
    {
        var mission = BuildMission(out _, out var substage);
        AddClue(mission);

        MissionStructureEditor.RenameNode(mission, ClueId, "New Clue Title", 5, null, null);

        var clue = substage.Clues.Single();
        clue.Id.Should().Be(ClueId);
        clue.Title.Should().Be("New Clue Title");
        clue.Text.Should().Be("Find it");   // text ?? clue.Text falls back to the existing text
    }

    [Fact]
    public void RenameNode_ReplacesClue_WithExplicitTextAndVisibility()
    {
        var mission = BuildMission(out _, out var substage);
        AddClue(mission);

        MissionStructureEditor.RenameNode(
            mission, ClueId, "New Title", 5, "Explicit text", ClueVisibilityPolicy.VisibleWhenSubstageStarts);

        var clue = substage.Clues.Single();
        clue.Text.Should().Be("Explicit text");           // text ?? clue.Text takes the provided text
        clue.Visibility.Should().Be(ClueVisibilityPolicy.VisibleWhenSubstageStarts);
    }

    [Fact]
    public void RenameNode_Missing_ThrowsNotFound()
    {
        var mission = BuildMission(out _, out _);
        var act = () => MissionStructureEditor.RenameNode(mission, 999, "x", 1, null, null);
        act.Should().Throw<ApplicationNotFoundException>();
    }

    // ── RemoveNode ───────────────────────────────────────────────────────────
    [Fact]
    public void RemoveNode_RemovesStage()
    {
        var mission = BuildMission(out var stage, out _);
        MissionStructureEditor.RemoveNode(mission, stage.Id);
        mission.Stages.Should().BeEmpty();
    }

    [Fact]
    public void RemoveNode_RemovesSubstage()
    {
        var mission = BuildMission(out var stage, out _);
        MissionStructureEditor.RemoveNode(mission, SubstageId);
        stage.Substages.Should().BeEmpty();
    }

    [Fact]
    public void RemoveNode_RemovesUnassociatedClue()
    {
        var mission = BuildMission(out _, out var substage);
        AddClue(mission);
        MissionStructureEditor.RemoveNode(mission, ClueId);
        substage.Clues.Should().BeEmpty();
    }

    [Fact]
    public void RemoveNode_ClueAssociatedWithTarget_ThrowsValidation()
    {
        var mission = BuildMission(out _, out var substage);
        var clue = AddClue(mission);
        AddTarget(substage);
        substage.AssociateClueWithTarget(TargetId, clue);

        var act = () => MissionStructureEditor.RemoveNode(mission, ClueId);
        act.Should().Throw<ApplicationValidationException>();
    }

    [Fact]
    public void RemoveNode_Missing_ThrowsNotFound()
    {
        var mission = BuildMission(out _, out _);
        var act = () => MissionStructureEditor.RemoveNode(mission, 999);
        act.Should().Throw<ApplicationNotFoundException>();
    }

    // ── AssignPlayMode ───────────────────────────────────────────────────────
    [Fact]
    public void AssignPlayMode_SameMode_IsNoOp()
    {
        var mission = BuildMission(out _, out var substage);
        MissionStructureEditor.AssignPlayMode(mission, StageId, SubstageId, SubstagePlayMode.TreasureHunt);
        substage.PlayMode.Should().Be(SubstagePlayMode.TreasureHunt);
    }

    [Fact]
    public void AssignPlayMode_TreasureToTrivia_SwapsPreservingClues()
    {
        var mission = BuildMission(out var stage, out _);
        AddClue(mission);

        MissionStructureEditor.AssignPlayMode(mission, StageId, SubstageId, SubstagePlayMode.Trivia);

        var swapped = stage.Substages.Single();
        swapped.Id.Should().Be(SubstageId);
        swapped.PlayMode.Should().Be(SubstagePlayMode.Trivia);
        swapped.Clues.Should().ContainSingle();
    }

    [Fact]
    public void AssignPlayMode_TriviaToTreasure_Swaps()
    {
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        var stage = mission.AddStage("Stage", 1);
        stage.Id = StageId;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTrivia("Trivia", 1));
        substage.Id = SubstageId;

        MissionStructureEditor.AssignPlayMode(mission, StageId, SubstageId, SubstagePlayMode.TreasureHunt);

        stage.Substages.Single().PlayMode.Should().Be(SubstagePlayMode.TreasureHunt);
    }

    [Fact]
    public void AssignPlayMode_MissingSubstage_ThrowsNotFound()
    {
        var mission = BuildMission(out _, out _);
        var act = () => MissionStructureEditor.AssignPlayMode(mission, StageId, 999, SubstagePlayMode.Trivia);
        act.Should().Throw<ApplicationNotFoundException>();
    }

    // ── UnassociateClueFromTarget ────────────────────────────────────────────
    [Fact]
    public void UnassociateClueFromTarget_NoClue_IsNoOp()
    {
        var mission = BuildMission(out _, out var substage);
        AddTarget(substage);

        MissionStructureEditor.UnassociateClueFromTarget(mission, StageId, SubstageId, TargetId);

        substage.Targets.Single().ClueId.Should().BeNull();
    }

    [Fact]
    public void UnassociateClueFromTarget_WithClue_ClearsAssociation()
    {
        var mission = BuildMission(out _, out var substage);
        var clue = AddClue(mission);
        AddTarget(substage);
        substage.AssociateClueWithTarget(TargetId, clue);

        MissionStructureEditor.UnassociateClueFromTarget(mission, StageId, SubstageId, TargetId);

        substage.Targets.Single(target => target.Id == TargetId).ClueId.Should().BeNull();
    }

    [Fact]
    public void UnassociateClueFromTarget_MissingTarget_ThrowsNotFound()
    {
        var mission = BuildMission(out _, out _);
        var act = () => MissionStructureEditor.UnassociateClueFromTarget(mission, StageId, SubstageId, 999);
        act.Should().Throw<ApplicationNotFoundException>();
    }
}
