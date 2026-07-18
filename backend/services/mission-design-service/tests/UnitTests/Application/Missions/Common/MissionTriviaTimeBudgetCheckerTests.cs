using umbral_backend.Application.Missions.Common;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.UnitTests.Application.Missions.Common;

// Drives MissionTriviaTimeBudgetChecker.Evaluate: within/over budget, per-substage double counting
// (a quiz reused by two substages counts twice), and a missing quiz id contributing no time.
public sealed class MissionTriviaTimeBudgetCheckerTests
{
    private static Mission BuildMission(int maxMinutes, params int[] selectedQuizIds)
    {
        var mission = Mission.Create("Mission", "Briefing", "Advanced", maxMinutes);
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 1;

        var order = 1;
        var nextId = 10;
        foreach (var quizId in selectedQuizIds)
        {
            var substage = mission.AddSubstage(stage.Id, Substage.CreateTrivia($"Trivia {order}", order));
            substage.Id = nextId++;
            substage.SelectTriviaQuiz(quizId);
            order++;
        }

        return mission;
    }

    [Fact]
    public void Evaluate_WhenTimersWithinBudget_NoFailures()
    {
        var mission = BuildMission(30, 100); // 1800s budget
        var timers = new Dictionary<int, int> { [100] = 60 };

        MissionTriviaTimeBudgetChecker.Evaluate(mission, timers).Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_WhenTimersExceedBudget_ReportsFailure()
    {
        var mission = BuildMission(1, 100); // 60s budget
        var timers = new Dictionary<int, int> { [100] = 90 };

        MissionTriviaTimeBudgetChecker.Evaluate(mission, timers).Should().ContainSingle()
            .Which.Should().Contain("exceeds the mission maximum time");
    }

    [Fact]
    public void Evaluate_SameQuizSelectedByTwoSubstages_CountsTimersTwice()
    {
        var mission = BuildMission(1, 100, 100); // 60s budget; quiz 100 selected by two substages
        var timers = new Dictionary<int, int> { [100] = 40 }; // 40s once fits, 80s twice does not

        MissionTriviaTimeBudgetChecker.Evaluate(mission, timers).Should().ContainSingle();
    }

    [Fact]
    public void Evaluate_WhenQuizIdMissingFromTimers_ContributesNoTime()
    {
        var mission = BuildMission(1, 100);

        MissionTriviaTimeBudgetChecker.Evaluate(mission, new Dictionary<int, int>()).Should().BeEmpty();
    }
}
