using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Domain.Entities;

public class MissionStructureTests
{
    private static Mission NewMission() => Mission.Create("Mission", "Briefing", "Advanced", 45);

    private static Stage StageWith(Mission mission, int stageId, int sequenceOrder = 1)
    {
        var stage = mission.AddStage($"Stage {stageId}", sequenceOrder);
        stage.Id = stageId;
        return stage;
    }

    private static Substage TreasureSubstageWith(Mission mission, int stageId, int substageId)
    {
        var substage = Substage.CreateTreasureHunt($"Substage {substageId}", 1);
        substage.Id = substageId;
        mission.AddSubstage(stageId, substage);
        return substage;
    }

    private static Substage TriviaSubstageWith(Mission mission, int stageId, int substageId)
    {
        var substage = Substage.CreateTrivia($"Substage {substageId}", 1);
        substage.Id = substageId;
        mission.AddSubstage(stageId, substage);
        return substage;
    }

    [Fact]
    public void AddStage_AppendsStageAndRaisesStructureEvents()
    {
        var mission = NewMission();
        mission.ClearDomainEvents();

        var stage = mission.AddStage("First Stage", 1);

        mission.Stages.Should().ContainSingle().Which.Should().BeSameAs(stage);
        mission.DomainEvents.Should().Contain(e => e is MissionNodeAddedEvent);
        mission.DomainEvents.Should().Contain(e => e is MissionStructureChangedEvent);
    }

    [Fact]
    public void Stages_AreOrderedBySequenceOrder()
    {
        var mission = NewMission();
        mission.AddStage("Second", 2);
        mission.AddStage("First", 1);

        mission.Stages.Select(s => s.Title).Should().ContainInOrder("First", "Second");
    }

    [Fact]
    public void AddSubstage_AttachesSubstageToStage()
    {
        var mission = NewMission();
        StageWith(mission, stageId: 10);

        var substage = TreasureSubstageWith(mission, stageId: 10, substageId: 100);

        mission.Stages.Single().Substages.Should().ContainSingle().Which.Should().BeSameAs(substage);
    }

    [Fact]
    public void AddSubstage_WhenStageMissing_Throws()
    {
        var mission = NewMission();

        var act = () => mission.AddSubstage(999, Substage.CreateTrivia("Sub", 1));

        act.Should().Throw<MissionNodeNotFoundException>();
    }

    [Fact]
    public void AddClue_AttachesClueUnderSubstage()
    {
        var mission = NewMission();
        StageWith(mission, 10);
        TreasureSubstageWith(mission, 10, 100);

        var clue = mission.AddClue(10, 100, Clue.Create("Hint", 1, "Look north"));

        mission.Stages.Single().Substages.Single().Clues.Should().ContainSingle().Which.Should().BeSameAs(clue);
    }

    [Fact]
    public void AddTarget_OnTreasureSubstage_RaisesTargetAddedEvent()
    {
        var mission = NewMission();
        StageWith(mission, 10);
        TreasureSubstageWith(mission, 10, 100);
        mission.ClearDomainEvents();

        var target = mission.AddTarget(10, 100, "Statue", "QR-1", 1, 4.711, -74.0721);

        target.QrCode.Should().Be("QR-1");
        mission.DomainEvents.Should().Contain(e => e is TargetAddedToSubstageEvent);
    }

    [Fact]
    public void AddTarget_DerivesScoreFromMissionDifficulty()
    {
        var mission = NewMission(); // Advanced => 50 * 3
        StageWith(mission, 10);
        TreasureSubstageWith(mission, 10, 100);

        var target = mission.AddTarget(10, 100, "Statue", "QR-1", 1, 4.711, -74.0721);

        target.Score.Points.Should().Be(150);
    }

    [Fact]
    public void UpdateDetails_WhenDifficultyChanges_RepricesEveryTarget()
    {
        var mission = NewMission(); // Advanced => 150
        StageWith(mission, 10);
        TreasureSubstageWith(mission, 10, 100);
        var target = mission.AddTarget(10, 100, "Statue", "QR-1", 1, 4.711, -74.0721);
        target.Score.Points.Should().Be(150);

        mission.UpdateDetails("Mission", "Briefing", "Beginner", 45); // Beginner => 50

        target.Score.Points.Should().Be(50);
    }

    [Fact]
    public void AddTarget_OnTriviaSubstage_Throws()
    {
        var mission = NewMission();
        StageWith(mission, 10);
        TriviaSubstageWith(mission, 10, 100);

        var act = () => mission.AddTarget(10, 100, "Statue", "QR-1", 1, 4.711, -74.0721);

        act.Should().Throw<SubstagePlayModeMismatchException>();
    }

    // QR uniqueness is mission-scoped because SessionOperations resolves a scan against the whole
    // mission snapshot: a duplicate anywhere makes that scan ambiguous mid-game.
    [Fact]
    public void AddTarget_WhenQrCodeDuplicatesTargetInSameSubstage_Throws()
    {
        var mission = NewMission();
        StageWith(mission, 10);
        TreasureSubstageWith(mission, 10, 100);
        mission.AddTarget(10, 100, "Statue", "QR-1", 1, 4.711, -74.0721);

        var act = () => mission.AddTarget(10, 100, "Fountain", "QR-1", 2, 4.712, -74.0722);

        act.Should().Throw<TargetQrCodeMustBeUniqueWithinMissionException>();
    }

    [Fact]
    public void AddTarget_WhenQrCodeDuplicatesTargetInAnotherStage_Throws()
    {
        var mission = NewMission();
        StageWith(mission, 10);
        TreasureSubstageWith(mission, 10, 100);
        mission.AddTarget(10, 100, "Statue", "QR-1", 1, 4.711, -74.0721);
        StageWith(mission, 20, 2);
        TreasureSubstageWith(mission, 20, 200);

        var act = () => mission.AddTarget(20, 200, "Fountain", "QR-1", 1, 4.712, -74.0722);

        act.Should().Throw<TargetQrCodeMustBeUniqueWithinMissionException>();
    }

    // Uniqueness matches the runtime's OrdinalIgnoreCase/trimmed resolution, so a differently-cased
    // or padded code is still the same code at scan time.
    [Theory]
    [InlineData("qr-1")]
    [InlineData("  QR-1  ")]
    public void AddTarget_WhenQrCodeDuplicatesIgnoringCaseAndWhitespace_Throws(string duplicateCode)
    {
        var mission = NewMission();
        StageWith(mission, 10);
        TreasureSubstageWith(mission, 10, 100);
        mission.AddTarget(10, 100, "Statue", "QR-1", 1, 4.711, -74.0721);

        var act = () => mission.AddTarget(10, 100, "Fountain", duplicateCode, 2, 4.712, -74.0722);

        act.Should().Throw<TargetQrCodeMustBeUniqueWithinMissionException>();
    }

    [Fact]
    public void AddTarget_WhenQrCodeIsDistinct_Succeeds()
    {
        var mission = NewMission();
        StageWith(mission, 10);
        TreasureSubstageWith(mission, 10, 100);
        mission.AddTarget(10, 100, "Statue", "QR-1", 1, 4.711, -74.0721);

        var target = mission.AddTarget(10, 100, "Fountain", "QR-2", 2, 4.712, -74.0722);

        target.QrCode.Should().Be("QR-2");
    }

    // A blank code is Target's own validation concern; the uniqueness guard must not pre-empt it
    // with a misleading failure.
    [Fact]
    public void AddTarget_WhenQrCodeIsBlank_ThrowsRequiredRatherThanUniqueness()
    {
        var mission = NewMission();
        StageWith(mission, 10);
        TreasureSubstageWith(mission, 10, 100);

        var act = () => mission.AddTarget(10, 100, "Statue", "   ", 1, 4.711, -74.0721);

        act.Should().Throw<TargetQrCodeRequiredException>();
    }

    [Fact]
    public void UpdateTarget_WhenQrCodeDuplicatesAnotherTarget_Throws()
    {
        var mission = NewMission();
        StageWith(mission, 10);
        TreasureSubstageWith(mission, 10, 100);
        var first = mission.AddTarget(10, 100, "Statue", "QR-1", 1, 4.711, -74.0721);
        first.Id = 700;
        var second = mission.AddTarget(10, 100, "Fountain", "QR-2", 2, 4.712, -74.0722);
        second.Id = 701;

        var act = () => mission.UpdateTarget(10, 100, 701, "Fountain", "QR-1", 2, 4.712, -74.0722, true);

        act.Should().Throw<TargetQrCodeMustBeUniqueWithinMissionException>();
    }

    // Regression: an update that keeps the target's own code must not clash with itself.
    [Fact]
    public void UpdateTarget_KeepingItsOwnQrCode_Succeeds()
    {
        var mission = NewMission();
        StageWith(mission, 10);
        TreasureSubstageWith(mission, 10, 100);
        var target = mission.AddTarget(10, 100, "Statue", "QR-1", 1, 4.711, -74.0721);
        target.Id = 700;

        var updated = mission.UpdateTarget(10, 100, 700, "Statue Renamed", "QR-1", 1, 4.711, -74.0721, true);

        updated.Name.Should().Be("Statue Renamed");
        updated.QrCode.Should().Be("QR-1");
    }

    [Fact]
    public void AssociateClueWithTarget_DoesNotChangeActivationState()
    {
        var mission = NewMission();
        StageWith(mission, 10);
        var substage = TreasureSubstageWith(mission, 10, 100);
        mission.AddTarget(10, 100, "Statue", "QR-1", 1, 4.711, -74.0721);
        mission.Activate();

        var clue = Clue.Create("Hint", 1, "Look north");
        clue.Id = 500;
        substage.AddClue(clue);
        mission.ClearDomainEvents();

        var target = substage.Targets.Single();
        mission.AssociateClueWithTarget(10, 100, target.Id, clue);

        target.ClueId.Should().Be(500);
        mission.ActivationState.Should().Be(MissionActivation.Ready);
        mission.DomainEvents.Should().Contain(e => e is ClueAssociatedWithTargetEvent);
        mission.DomainEvents.Should().NotContain(e => e is MissionActivatedEvent);
    }

    [Fact]
    public void AssociateClueWithTarget_WhenClueFromAnotherSubstage_Throws()
    {
        var mission = NewMission();
        StageWith(mission, 10);
        TreasureSubstageWith(mission, 10, 100);
        var target = mission.AddTarget(10, 100, "Statue", "QR-1", 1, 4.711, -74.0721);
        target.Id = 700;

        var foreignClue = Clue.Create("Hint", 1, "Look north");
        foreignClue.Id = 900;

        var act = () => mission.AssociateClueWithTarget(10, 100, 700, foreignClue);

        act.Should().Throw<ClueMustBelongToSameSubstageException>();
    }

    [Fact]
    public void Activate_WhenRuntimePlanComplete_MarksReadyAndRaisesActivatedEvent()
    {
        var mission = BuildReadyTreasureMission();
        mission.ClearDomainEvents();

        mission.Activate();

        mission.ActivationState.Should().Be(MissionActivation.Ready);
        mission.IsActive.Should().BeTrue();
        mission.DomainEvents.Should().ContainSingle(e => e is MissionActivatedEvent);
    }

    [Fact]
    public void Activate_WhenStageHasNoSubstage_Throws()
    {
        var mission = NewMission();
        StageWith(mission, 10);

        var act = mission.Activate;

        act.Should().Throw<MissionNotReadyForActivationException>();
    }

    [Fact]
    public void Activate_WhenTreasureSubstageHasNoActiveTarget_Throws()
    {
        var mission = NewMission();
        StageWith(mission, 10);
        TreasureSubstageWith(mission, 10, 100);
        mission.AddTarget(10, 100, "Statue", "QR-1", 1, 4.711, -74.0721, isActive: false);

        var act = mission.Activate;

        act.Should().Throw<MissionNotReadyForActivationException>();
    }

    [Fact]
    public void Activate_WhenTriviaSubstageHasNoQuiz_Throws()
    {
        var mission = NewMission();
        StageWith(mission, 10);
        TriviaSubstageWith(mission, 10, 100);

        var act = mission.Activate;

        act.Should().Throw<MissionNotReadyForActivationException>();
    }

    [Fact]
    public void Activate_TriviaMissionWithSelectedQuiz_BecomesReady()
    {
        var mission = NewMission();
        StageWith(mission, 10);
        TriviaSubstageWith(mission, 10, 100);
        mission.SelectTriviaQuiz(10, 100, triviaQuizId: 42);

        mission.Activate();

        mission.ActivationState.Should().Be(MissionActivation.Ready);
    }

    [Fact]
    public void Deactivate_AfterActivation_MarksInactive()
    {
        var mission = BuildReadyTreasureMission();
        mission.Activate();

        mission.Deactivate(DateTimeOffset.UtcNow);

        mission.IsActive.Should().BeFalse();
        mission.ActivationState.Should().Be(MissionActivation.Inactive);
    }

    private static Mission BuildReadyTreasureMission()
    {
        var mission = NewMission();
        StageWith(mission, 10);
        TreasureSubstageWith(mission, 10, 100);
        mission.AddTarget(10, 100, "Statue", "QR-1", 1, 4.711, -74.0721);
        return mission;
    }
}
