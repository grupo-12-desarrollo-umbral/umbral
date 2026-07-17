using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Domain.Services;

public class MissionActivationPolicyRuntimePlanTests
{
    private static Mission NewMission() => Mission.Create("Mission", "Briefing", "Advanced", 45);

    [Fact]
    public void EvaluateReadiness_WhenNoStages_ReportsMissingStage()
    {
        var mission = NewMission();

        var failures = MissionActivationPolicy.EvaluateReadiness(mission);

        failures.Should().ContainSingle().Which.Should().Contain("at least one stage");
        MissionActivationPolicy.SatisfiesRuntimePlan(mission).Should().BeFalse();
    }

    [Fact]
    public void EvaluateReadiness_WhenStageHasNoSubstage_ReportsMissingSubstage()
    {
        var mission = NewMission();
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 1;

        var failures = MissionActivationPolicy.EvaluateReadiness(mission);

        failures.Should().Contain(f => f.Contains("at least one substage"));
    }

    [Fact]
    public void EvaluateReadiness_WhenTreasureSubstageMissingTarget_ReportsMissingActiveTarget()
    {
        var mission = NewMission();
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 1;
        var substage = Substage.CreateTreasureHunt("Sub", 1);
        substage.Id = 2;
        mission.AddSubstage(1, substage);

        var failures = MissionActivationPolicy.EvaluateReadiness(mission);

        failures.Should().Contain(f => f.Contains("active target"));
    }

    [Fact]
    public void EvaluateReadiness_WhenTreasureTargetInactive_ReportsMissingActiveTarget()
    {
        var mission = NewMission();
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 1;
        var substage = Substage.CreateTreasureHunt("Sub", 1);
        substage.Id = 2;
        mission.AddSubstage(1, substage);
        mission.AddTarget(1, 2, "Statue", "QR-1", 1, 4.711, -74.0721, isActive: false);

        var failures = MissionActivationPolicy.EvaluateReadiness(mission);

        failures.Should().Contain(f => f.Contains("active target"));
    }

    [Fact]
    public void EvaluateReadiness_WhenTriviaSubstageHasNoQuiz_ReportsMissingQuiz()
    {
        var mission = NewMission();
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 1;
        var substage = Substage.CreateTrivia("Sub", 1);
        substage.Id = 2;
        mission.AddSubstage(1, substage);

        var failures = MissionActivationPolicy.EvaluateReadiness(mission);

        failures.Should().Contain(f => f.Contains("published trivia quiz"));
    }

    [Fact]
    public void EvaluateReadiness_WhenFullTreasurePlanReady_ReturnsNoFailures()
    {
        var mission = NewMission();
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 1;
        var substage = Substage.CreateTreasureHunt("Sub", 1);
        substage.Id = 2;
        mission.AddSubstage(1, substage);
        mission.AddTarget(1, 2, "Statue", "QR-1", 1, 4.711, -74.0721);

        MissionActivationPolicy.EvaluateReadiness(mission).Should().BeEmpty();
        MissionActivationPolicy.SatisfiesRuntimePlan(mission).Should().BeTrue();
    }

    // Duplicate QR codes can no longer be authored, so these build them the only way they now occur:
    // straight onto the substage, as a mission snapshotted before the aggregate guard existed would hold.
    [Fact]
    public void EvaluateReadiness_WhenTwoTargetsShareQrCode_ReportsDuplicate()
    {
        var mission = NewMission();
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 1;
        var substage = Substage.CreateTreasureHunt("Sub", 1);
        substage.Id = 2;
        mission.AddSubstage(1, substage);
        substage.AddTarget("Statue", "QR-1", 1, 50, 4.711, -74.0721);
        substage.AddTarget("Fountain", "QR-1", 2, 50, 4.712, -74.0722);

        var failures = MissionActivationPolicy.EvaluateReadiness(mission);

        failures.Should().ContainSingle().Which.Should().Contain("QR-1").And.Contain("unique");
        MissionActivationPolicy.SatisfiesRuntimePlan(mission).Should().BeFalse();
    }

    [Fact]
    public void EvaluateReadiness_WhenTargetsShareQrCodeAcrossStages_ReportsDuplicate()
    {
        var mission = NewMission();
        var firstStage = mission.AddStage("Stage 1", 1);
        firstStage.Id = 1;
        var firstSubstage = Substage.CreateTreasureHunt("Sub 1", 1);
        firstSubstage.Id = 2;
        mission.AddSubstage(1, firstSubstage);
        firstSubstage.AddTarget("Statue", "QR-1", 1, 50, 4.711, -74.0721);

        var secondStage = mission.AddStage("Stage 2", 2);
        secondStage.Id = 3;
        var secondSubstage = Substage.CreateTreasureHunt("Sub 2", 1);
        secondSubstage.Id = 4;
        mission.AddSubstage(3, secondSubstage);
        secondSubstage.AddTarget("Fountain", "qr-1", 1, 50, 4.712, -74.0722);

        var failures = MissionActivationPolicy.EvaluateReadiness(mission);

        failures.Should().ContainSingle().Which.Should().Contain("unique");
    }

    // One failure per duplicated code, not one per offending target.
    [Fact]
    public void EvaluateReadiness_WhenThreeTargetsShareOneQrCode_ReportsSingleFailure()
    {
        var mission = NewMission();
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 1;
        var substage = Substage.CreateTreasureHunt("Sub", 1);
        substage.Id = 2;
        mission.AddSubstage(1, substage);
        substage.AddTarget("A", "QR-1", 1, 50, 4.711, -74.0721);
        substage.AddTarget("B", "QR-1", 2, 50, 4.712, -74.0722);
        substage.AddTarget("C", "QR-1", 3, 50, 4.713, -74.0723);

        MissionActivationPolicy.EvaluateReadiness(mission).Should().ContainSingle();
    }

    [Fact]
    public void EvaluateReadiness_WhenFullTriviaPlanReady_ReturnsNoFailures()
    {
        var mission = NewMission();
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 1;
        var substage = Substage.CreateTrivia("Sub", 1);
        substage.Id = 2;
        mission.AddSubstage(1, substage);
        mission.SelectTriviaQuiz(1, 2, 42);

        MissionActivationPolicy.EvaluateReadiness(mission).Should().BeEmpty();
    }
}
