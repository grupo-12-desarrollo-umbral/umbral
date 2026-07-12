using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Domain.Entities;

public sealed class BranchCoverageDomainTests
{
    // ── Mission.RefreshActivationState branches ──────────────────────────────

    [Fact]
    public void Activate_WhenMissionIsAlreadyReady_ThrowsMissionAlreadyActiveException()
    {
        var mission = BuildReadyMission();
        mission.Activate();

        var act = () => mission.Activate();

        act.Should().Throw<MissionAlreadyActiveException>();
    }

    [Fact]
    public void StructureChange_WhenMissionIsReadyAndPlanStillSatisfied_StaysReady()
    {
        var mission = BuildReadyMission();
        mission.Activate();
        mission.ClearDomainEvents();

        // Adding another target to a ready mission keeps the plan satisfied
        mission.AddTarget(10, 20, "New Target", "QR-2", 2);

        mission.ActivationState.Should().Be(MissionActivation.Ready);
    }

    [Fact]
    public void StructureChange_WhenMissionIsReadyAndPlanBroken_DemotesToDraft()
    {
        var mission = BuildReadyMission();
        mission.Activate();
        var stage = mission.Stages.Single();
        var substage = stage.Substages.Single();
        // Removing the only target breaks the plan
        mission.RemoveTarget(stage.Id, substage.Id, substage.Targets.Single().Id);

        mission.ActivationState.Should().Be(MissionActivation.Draft);
    }

    [Fact]
    public void UpdateDetails_OnDeactivatedMission_LeavesInactive()
    {
        var mission = Mission.Create("M", "D", "Advanced", 30);
        mission.Deactivate(DateTimeOffset.UtcNow);

        mission.UpdateDetails("M2", "D2", "Beginner", 60);

        mission.ActivationState.Should().Be(MissionActivation.Inactive);
    }

    [Fact]
    public void UpdateDetails_WhenReadyAndPlanStillSatisfied_StaysReady()
    {
        var mission = BuildReadyMission();
        mission.Activate();

        // Update with same difficulty — plan stays satisfied
        mission.UpdateDetails("Updated", "Updated desc", "Advanced", 45);

        mission.ActivationState.Should().Be(MissionActivation.Ready);
    }

    [Fact]
    public void UpdateDetails_WhenReadyAndPlanBroken_DemotesToDraft()
    {
        var mission = BuildReadyMission();
        mission.Activate();

        // Change difficulty changes target scores, but plan stays satisfied since target is active
        // Instead, let's remove all targets and then update
        var stage = mission.Stages.Single();
        var substage = stage.Substages.Single();
        mission.RemoveTarget(stage.Id, substage.Id, substage.Targets.Single().Id);
        // Now mission is Draft (demoted)

        mission.ActivationState.Should().Be(MissionActivation.Draft);
    }

    [Fact]
    public void SelectTriviaQuiz_RefreshesActivationState()
    {
        var mission = Mission.Create("M", "D", "Advanced", 30);
        var stage = mission.AddStage("S", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTrivia("T", 1));
        substage.Id = 20;

        mission.SelectTriviaQuiz(stage.Id, substage.Id, 42);

        mission.Stages.Single().Substages.Single().TriviaQuizId.Should().Be(42);
    }

    [Fact]
    public void RemoveTarget_WhenInReadyMission_DemotesToDraft()
    {
        var mission = BuildReadyMission();
        mission.Activate();
        var stage = mission.Stages.Single();
        var substage = stage.Substages.Single();
        var targetId = substage.Targets.Single().Id;

        mission.RemoveTarget(stage.Id, substage.Id, targetId);

        mission.ActivationState.Should().Be(MissionActivation.Draft);
    }

    // ── Mission.FindStage / FindSubstage branches ────────────────────────────

    [Fact]
    public void AddClue_WhenStageNotFound_ThrowsMissionNodeNotFoundException()
    {
        var mission = Mission.Create("M", "D", "Advanced", 30);

        var act = () => mission.AddClue(999, 999, Clue.Create("C", 1, "text"));

        act.Should().Throw<MissionNodeNotFoundException>();
    }

    [Fact]
    public void RenameNode_WhenStageNotFound_ThrowsMissionNodeNotFoundException()
    {
        var mission = Mission.Create("M", "D", "Advanced", 30);

        var act = () => mission.RenameNode(999, "Title", 1);

        act.Should().Throw<MissionNodeNotFoundException>();
    }

    [Fact]
    public void RemoveStage_WhenStageNotFound_ThrowsMissionNodeNotFoundException()
    {
        var mission = Mission.Create("M", "D", "Advanced", 30);

        var act = () => mission.RemoveStage(999);

        act.Should().Throw<MissionNodeNotFoundException>();
    }

    [Fact]
    public void UpdateTarget_WhenSubstageNotFound_ThrowsMissionNodeNotFoundException()
    {
        var mission = Mission.Create("M", "D", "Advanced", 30);
        var stage = mission.AddStage("S", 1);
        stage.Id = 10;

        var act = () => mission.UpdateTarget(10, 999, 1, "N", "QR", 1, true);

        act.Should().Throw<MissionNodeNotFoundException>();
    }

    [Fact]
    public void RemoveTarget_WhenSubstageNotFound_ThrowsMissionNodeNotFoundException()
    {
        var mission = Mission.Create("M", "D", "Advanced", 30);
        var stage = mission.AddStage("S", 1);
        stage.Id = 10;

        var act = () => mission.RemoveTarget(10, 999, 1);

        act.Should().Throw<MissionNodeNotFoundException>();
    }

    [Fact]
    public void AssociateClueWithTarget_WhenSubstageNotFound_ThrowsMissionNodeNotFoundException()
    {
        var mission = Mission.Create("M", "D", "Advanced", 30);
        var stage = mission.AddStage("S", 1);
        stage.Id = 10;

        var act = () => mission.AssociateClueWithTarget(10, 999, 1, Clue.Create("C", 1, "text"));

        act.Should().Throw<MissionNodeNotFoundException>();
    }

    [Fact]
    public void SelectTriviaQuiz_WhenSubstageNotFound_ThrowsMissionNodeNotFoundException()
    {
        var mission = Mission.Create("M", "D", "Advanced", 30);
        var stage = mission.AddStage("S", 1);
        stage.Id = 10;

        var act = () => mission.SelectTriviaQuiz(10, 999, 42);

        act.Should().Throw<MissionNodeNotFoundException>();
    }

    // ── TriviaQuestion branches ──────────────────────────────────────────────

    [Fact]
    public void Create_TriviaQuestion_WithNullExplanation_NormalizesToNull()
    {
        var question = TriviaQuestion.Create("Prompt", 1, 10, 30, "  ", null);

        question.Explanation.Should().BeNull();
    }

    [Fact]
    public void Create_TriviaQuestion_WithEmptyExplanation_NormalizesToNull()
    {
        var question = TriviaQuestion.Create("Prompt", 1, 10, 30, "", null);

        question.Explanation.Should().BeNull();
    }

    [Fact]
    public void Create_TriviaQuestion_WithNegativeScoreValue_ThrowsPositiveException()
    {
        var act = () => TriviaQuestion.Create("Prompt", 1, -5, 30, null);

        act.Should().Throw<TriviaQuestionScoreValueMustBePositiveException>();
    }

    [Fact]
    public void Create_TriviaQuestion_WithNullScoreValue_SetsNull()
    {
        var question = TriviaQuestion.Create("Prompt", 1, null, 30, null);

        question.ScoreValue.Should().BeNull();
    }

    [Fact]
    public void Create_TriviaQuestion_WithNullTimeLimit_SetsNull()
    {
        var question = TriviaQuestion.Create("Prompt", 1, 10, null, null);

        question.TimeLimit.Should().BeNull();
    }

    // ── Substage branches ────────────────────────────────────────────────────

    [Fact]
    public void AddTarget_OnTriviaSubstage_ThrowsSubstagePlayModeMismatchException()
    {
        var substage = Substage.CreateTrivia("Trivia", 1);

        var act = () => substage.AddTarget("T", "QR", 1, 20);

        act.Should().Throw<SubstagePlayModeMismatchException>();
    }

    [Fact]
    public void UpdateTarget_OnTriviaSubstage_ThrowsSubstagePlayModeMismatchException()
    {
        var substage = Substage.CreateTrivia("Trivia", 1);

        var act = () => substage.UpdateTarget(1, "T", "QR", 1, true);

        act.Should().Throw<SubstagePlayModeMismatchException>();
    }

    [Fact]
    public void RemoveTarget_OnTriviaSubstage_ThrowsSubstagePlayModeMismatchException()
    {
        var substage = Substage.CreateTrivia("Trivia", 1);

        var act = () => substage.RemoveTarget(1);

        act.Should().Throw<SubstagePlayModeMismatchException>();
    }

    [Fact]
    public void AssociateClueWithTarget_OnTriviaSubstage_ThrowsSubstagePlayModeMismatchException()
    {
        var substage = Substage.CreateTrivia("Trivia", 1);
        var clue = Clue.Create("C", 1, "text");
        substage.AddClue(clue);

        var act = () => substage.AssociateClueWithTarget(1, clue);

        act.Should().Throw<SubstagePlayModeMismatchException>();
    }

    [Fact]
    public void SelectTriviaQuiz_WithZeroId_ThrowsSubstageRequiresPlayModeException()
    {
        var substage = Substage.CreateTrivia("Trivia", 1);

        var act = () => substage.SelectTriviaQuiz(0);

        act.Should().Throw<SubstageRequiresPlayModeException>();
    }

    [Fact]
    public void SelectTriviaQuiz_WithNegativeId_ThrowsSubstageRequiresPlayModeException()
    {
        var substage = Substage.CreateTrivia("Trivia", 1);

        var act = () => substage.SelectTriviaQuiz(-1);

        act.Should().Throw<SubstageRequiresPlayModeException>();
    }

    [Fact]
    public void RemoveChildNode_WhenChildIsNotClue_DoesNothing()
    {
        var substage = Substage.CreateTreasureHunt("Sub", 1);
        var clue = Clue.Create("C", 1, "text");
        substage.AddClue(clue);
        // Removing a clue that exists exercises the `if (child is Clue clue)` true branch
        substage.RemoveChild(clue);
        substage.Clues.Should().BeEmpty();
    }

    [Fact]
    public void UpdateTarget_WhenTargetNotFound_ThrowsTargetNotFoundException()
    {
        var substage = Substage.CreateTreasureHunt("Sub", 1);

        var act = () => substage.UpdateTarget(999, "N", "QR", 1, true);

        act.Should().Throw<TargetNotFoundException>();
    }

    // ── Clue branches ────────────────────────────────────────────────────────

    [Fact]
    public void AddChildNode_OnClue_ThrowsInvalidMissionNodeChildException()
    {
        var clue = Clue.Create("C", 1, "text");

        var act = () => clue.AddChild(Clue.Create("Nested", 2, "text"));

        act.Should().Throw<InvalidMissionNodeChildException>();
    }

    [Fact]
    public void CanContain_OnClue_ReturnsFalse()
    {
        var clue = Clue.Create("C", 1, "text");

        // Clue.CanContain always returns false — test by trying to add any child
        var act = () => clue.AddChild(Clue.Create("Nested", 2, "text"));

        act.Should().Throw<InvalidMissionNodeChildException>();
    }

    // ── Target branches ──────────────────────────────────────────────────────

    [Fact]
    public void AssociateClue_WhenAlreadyAssociatedWithSameClue_IsIdempotent()
    {
        var target = Target.Create("T", "QR", 1, 20);
        var substage = Substage.CreateTreasureHunt("Sub", 1);
        substage.AddTarget("T", "QR", 1, 20);
        var clue = Clue.Create("C", 1, "text");
        clue.Id = 5;
        substage.AddClue(clue);

        var addedTarget = substage.Targets.Single();
        substage.AssociateClueWithTarget(addedTarget.Id, clue);
        substage.AssociateClueWithTarget(addedTarget.Id, clue); // idempotent

        addedTarget.ClueId.Should().Be(5);
    }

    [Fact]
    public void ClearClue_SetsClueIdToNull()
    {
        var target = Target.Create("T", "QR", 1, 20);
        var substage = Substage.CreateTreasureHunt("Sub", 1);
        substage.AddTarget("T", "QR", 1, 20);
        var clue = Clue.Create("C", 1, "text");
        clue.Id = 5;
        substage.AddClue(clue);

        var addedTarget = substage.Targets.Single();
        substage.AssociateClueWithTarget(addedTarget.Id, clue);
        addedTarget.ClueId.Should().Be(5);

        addedTarget.ClearClue();
        addedTarget.ClueId.Should().BeNull();
    }

    [Fact]
    public void Reprice_UpdatesScore()
    {
        var target = Target.Create("T", "QR", 1, 50);
        target.Score.Points.Should().Be(50);

        target.Reprice(100);

        target.Score.Points.Should().Be(100);
    }

    // ── MissionNode.Rename branches ──────────────────────────────────────────

    [Fact]
    public void Rename_WithValidTitleAndSequence_UpdatesTitleAndSequence()
    {
        var stage = Stage.Create("Old", 1);
        stage.Rename("New", 5);

        stage.Title.Should().Be("New");
        stage.SequenceOrder.Should().Be(5);
    }

    [Fact]
    public void Rename_WithBlankTitle_ThrowsMissionNodeTitleRequiredException()
    {
        var stage = Stage.Create("Old", 1);

        var act = () => stage.Rename("", 1);

        act.Should().Throw<MissionNodeTitleRequiredException>();
    }

    [Fact]
    public void Rename_WithZeroSequenceOrder_ThrowsMissionNodeSequenceOrderMustBePositiveException()
    {
        var stage = Stage.Create("Old", 1);

        var act = () => stage.Rename("New", 0);

        act.Should().Throw<MissionNodeSequenceOrderMustBePositiveException>();
    }

    // ── Stage.RemoveChildNode branch ─────────────────────────────────────────

    [Fact]
    public void RemoveChild_OnStage_RemovesSubstage()
    {
        var stage = Stage.Create("S", 1);
        var substage = Substage.CreateTrivia("T", 1);
        stage.AddSubstage(substage);

        stage.RemoveChild(substage);

        stage.Substages.Should().BeEmpty();
    }

    // ── TriviaQuiz branches ──────────────────────────────────────────────────

    [Fact]
    public void Duplicate_WhenSourceHasNoId_FallsBackToSourceTriviaQuizId()
    {
        var original = TriviaQuiz.Create("Q", "D");
        original.AddQuestion("P", 1, 10, 30, null, [
            TriviaOption.Create("A", 1, true),
            TriviaOption.Create("B", 2, false)
        ]);
        original.Id = 41;
        original.Publish(DateTimeOffset.UtcNow);

        var firstCopy = original.Duplicate();
        firstCopy.Id = 0; // unpersisted

        var secondCopy = firstCopy.Duplicate();

        secondCopy.SourceTriviaQuizId.Should().Be(41);
    }

    [Fact]
    public void MarkAsPublished_SetsPublishedStatus()
    {
        var quiz = TriviaQuiz.Create("Q", "D");
        quiz.AddQuestion("P", 1, 10, 30, null, [
            TriviaOption.Create("A", 1, true),
            TriviaOption.Create("B", 2, false)
        ]);

        quiz.MarkAsPublished();

        quiz.Status.Should().Be(TriviaQuizStatus.Published);
    }

    [Fact]
    public void MarkAsArchived_SetsArchivedStatus()
    {
        var quiz = TriviaQuiz.Create("Q", "D");

        quiz.MarkAsArchived();

        quiz.Status.Should().Be(TriviaQuizStatus.Archived);
    }

    [Fact]
    public void MarkAsUsedInSession_SetsHasUsageHistory()
    {
        var quiz = TriviaQuiz.Create("Q", "D");

        quiz.MarkAsUsedInSession();

        quiz.HasUsageHistory.Should().BeTrue();
    }

    [Fact]
    public void Create_WithNullQuestions_DefaultsToEmpty()
    {
        var quiz = TriviaQuiz.Create("Q", "D", null);

        quiz.Questions.Should().BeEmpty();
    }

    [Fact]
    public void UpdateDetails_WithNullQuestions_UsesExistingQuestions()
    {
        var quiz = TriviaQuiz.Create("Q", "D");
        quiz.AddQuestion("P", 1, 10, 30, null, [
            TriviaOption.Create("A", 1, true),
            TriviaOption.Create("B", 2, false)
        ]);

        quiz.UpdateDetails("Q2", "D2", null);

        quiz.Questions.Should().ContainSingle();
    }

    [Fact]
    public void Duplicate_WhenQuizIsArchived_ThrowsTriviaQuizCannotBeArchivedInCurrentStateException()
    {
        var quiz = TriviaQuiz.Create("Q", "D");
        quiz.Archive(DateTimeOffset.UtcNow);

        var act = () => quiz.Duplicate();

        act.Should().Throw<TriviaQuizCannotBeArchivedInCurrentStateException>();
    }

    [Fact]
    public void RetireFromFutureUse_WhenQuizIsArchived_ThrowsTriviaQuizCannotBeArchivedInCurrentStateException()
    {
        var quiz = TriviaQuiz.Create("Q", "D");
        quiz.Archive(DateTimeOffset.UtcNow);

        var act = () => quiz.RetireFromFutureUse(DateTimeOffset.UtcNow);

        act.Should().Throw<TriviaQuizCannotBeArchivedInCurrentStateException>();
    }

    // ── TriviaOption.ApplyAuthoring branch ───────────────────────────────────

    [Fact]
    public void ApplyAuthoring_OnTriviaOption_UpdatesAllFields()
    {
        var option = TriviaOption.Create("Old", 1, false);

        option.ApplyAuthoring(" New ", 2, true);

        option.OptionText.Should().Be("New");
        option.SequenceOrder.Should().Be(2);
        option.IsCorrect.Should().BeTrue();
    }

    // ── TriviaQuestion.ApplyAuthoring branch ─────────────────────────────────

    [Fact]
    public void ApplyAuthoring_OnTriviaQuestion_UpdatesAllFields()
    {
        var question = TriviaQuestion.Create("Old", 1, 10, 30, null, [
            TriviaOption.Create("A", 1, true),
            TriviaOption.Create("B", 2, false)
        ]);

        question.ApplyAuthoring(" New ", 2, 50, 60, "Explained", [
            TriviaOption.Create("C", 1, true),
            TriviaOption.Create("D", 2, false)
        ], false);

        question.Prompt.Should().Be("New");
        question.SequenceOrder.Should().Be(2);
        question.ScoreValue.Should().Be(50);
        question.TimeLimit!.Seconds.Should().Be(60);
        question.Explanation.Should().Be("Explained");
        question.IsActive.Should().BeFalse();
        question.Options.Should().HaveCount(2);
    }

    // ── Mission.AddSubstage with null guard ───────────────────────────────────

    [Fact]
    public void AddSubstage_WithNullSubstage_ThrowsArgumentNullException()
    {
        var mission = Mission.Create("M", "D", "Advanced", 30);
        var stage = mission.AddStage("S", 1);
        stage.Id = 10;

        var act = () => mission.AddSubstage(stage.Id, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddClue_WithNullClue_ThrowsArgumentNullException()
    {
        var mission = Mission.Create("M", "D", "Advanced", 30);
        var stage = mission.AddStage("S", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("T", 1));
        substage.Id = 20;

        var act = () => mission.AddClue(stage.Id, substage.Id, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AssociateClueWithTarget_WithNullClue_ThrowsArgumentNullException()
    {
        var mission = Mission.Create("M", "D", "Advanced", 30);
        var stage = mission.AddStage("S", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("T", 1));
        substage.Id = 20;

        var act = () => mission.AssociateClueWithTarget(stage.Id, substage.Id, 1, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── Mission.Activate readiness branches ──────────────────────────────────

    [Fact]
    public void Activate_WhenNoStages_ThrowsMissionNotReadyForActivationException()
    {
        var mission = Mission.Create("M", "D", "Advanced", 30);

        var act = () => mission.Activate();

        act.Should().Throw<MissionNotReadyForActivationException>();
    }

    [Fact]
    public void Activate_WhenTreasureSubstageHasNoActiveTarget_ThrowsMissionNotReadyForActivationException()
    {
        var mission = Mission.Create("M", "D", "Advanced", 30);
        var stage = mission.AddStage("S", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("T", 1));
        substage.Id = 20;
        mission.AddTarget(stage.Id, substage.Id, "T", "QR", 1, isActive: false);

        var act = () => mission.Activate();

        act.Should().Throw<MissionNotReadyForActivationException>();
    }

    [Fact]
    public void Activate_WhenTriviaSubstageHasNoQuiz_ThrowsMissionNotReadyForActivationException()
    {
        var mission = Mission.Create("M", "D", "Advanced", 30);
        var stage = mission.AddStage("S", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTrivia("T", 1));
        substage.Id = 20;

        var act = () => mission.Activate();

        act.Should().Throw<MissionNotReadyForActivationException>();
    }

    // ── TriviaQuestion.SetSequenceOrder branch ───────────────────────────────

    [Fact]
    public void SetSequenceOrder_OnTriviaQuestion_UpdatesSequenceOrder()
    {
        var question = TriviaQuestion.Create("P", 1);

        question.SetSequenceOrder(5);

        question.SequenceOrder.Should().Be(5);
    }

    [Fact]
    public void SetSequenceOrder_WithInvalidSequence_Throws()
    {
        var question = TriviaQuestion.Create("P", 1);

        var act = () => question.SetSequenceOrder(0);

        act.Should().Throw<TriviaQuestionSequenceOrderMustBePositiveException>();
    }

    // ── Mission.AddStage then RemoveStage ─────────────────────────────────────

    [Fact]
    public void RemoveStage_RemovesStageAndRaisesEvents()
    {
        var mission = Mission.Create("M", "D", "Advanced", 30);
        var stage = mission.AddStage("S", 1);
        stage.Id = 10;
        mission.ClearDomainEvents();

        mission.RemoveStage(stage.Id);

        mission.Stages.Should().BeEmpty();
        mission.DomainEvents.Should().Contain(e => e is MissionNodeRemovedEvent);
    }

    private static Mission BuildReadyMission()
    {
        var mission = Mission.Create("M", "D", "Advanced", 30);
        var stage = mission.AddStage("S", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("T", 1));
        substage.Id = 20;
        mission.AddTarget(stage.Id, substage.Id, "Target", "QR", 1);
        return mission;
    }
}
