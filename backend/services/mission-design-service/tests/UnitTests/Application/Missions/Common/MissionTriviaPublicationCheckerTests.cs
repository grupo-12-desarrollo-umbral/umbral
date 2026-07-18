using umbral_backend.Application.Missions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Application.Missions.Common;

// Drives both compound conditions in MissionTriviaPublicationChecker.Evaluate — the
// (non-trivia OR no-selection) skip and the (quiz-missing OR not-published) failure — plus
// CollectTriviaQuizIds distinct-id collection.
public sealed class MissionTriviaPublicationCheckerTests
{
    private const int QuizId = 100;

    // A mission with: a trivia substage that selected quiz 100, a treasure substage (non-trivia,
    // skipped), and a trivia substage with no selection (skipped).
    private static Mission BuildMission()
    {
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 30);
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 1;

        var selectedTrivia = mission.AddSubstage(stage.Id, Substage.CreateTrivia("Trivia A", 1));
        selectedTrivia.Id = 10;
        selectedTrivia.SelectTriviaQuiz(QuizId);

        var treasure = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Hunt", 2));
        treasure.Id = 11;

        var unselectedTrivia = mission.AddSubstage(stage.Id, Substage.CreateTrivia("Trivia B", 3));
        unselectedTrivia.Id = 12;

        return mission;
    }

    [Fact]
    public void CollectTriviaQuizIds_ReturnsDistinctSelectedIds()
    {
        MissionTriviaPublicationChecker.CollectTriviaQuizIds(BuildMission())
            .Should().BeEquivalentTo(new[] { QuizId });
    }

    [Fact]
    public void Evaluate_QuizPublished_NoFailures()
    {
        var statuses = new Dictionary<int, TriviaQuizStatus> { [QuizId] = TriviaQuizStatus.Published };

        MissionTriviaPublicationChecker.Evaluate(BuildMission(), statuses).Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_QuizArchived_ReportsFailure()
    {
        var statuses = new Dictionary<int, TriviaQuizStatus> { [QuizId] = TriviaQuizStatus.Archived };

        MissionTriviaPublicationChecker.Evaluate(BuildMission(), statuses).Should().ContainSingle();
    }

    [Fact]
    public void Evaluate_QuizMissingFromStatuses_ReportsFailure()
    {
        MissionTriviaPublicationChecker.Evaluate(BuildMission(), new Dictionary<int, TriviaQuizStatus>())
            .Should().ContainSingle();
    }
}
