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
