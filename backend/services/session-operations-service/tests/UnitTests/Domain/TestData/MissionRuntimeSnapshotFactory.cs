using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.SessionOperations.UnitTests.Domain.TestData;

internal static class MissionRuntimeSnapshotFactory
{
    internal static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(int maximumTimeMinutes = 45)
    {
        var treasureSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1, winnerScore: 100);
        var stage = StageSnapshot.Create("Stage One", 1, [treasureSubstage]);

        return MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Museum Hunt",
            MaximumTime.Create(maximumTimeMinutes),
            [stage],
            [CreateTarget(treasureSubstage.SubstageSnapshotId)],
            []);
    }

    internal static MissionRuntimeSnapshot CreateTriviaSnapshot(int maximumTimeMinutes = 10)
    {
        return CreateMixedSnapshot(maximumTimeMinutes, triviaQuestionCount: 1);
    }

    internal static MissionRuntimeSnapshot CreateTriviaSnapshotWithThreeQuestions(int maximumTimeMinutes = 10)
    {
        return CreateMixedSnapshot(maximumTimeMinutes, triviaQuestionCount: 3);
    }

    internal static TargetSnapshot CreateTarget(Guid substageSnapshotId, string qrCode = "QR-001", int sequenceOrder = 1)
    {
        return TargetSnapshot.Create(
            substageSnapshotId,
            "Main Exhibit",
            qrCode,
            sequenceOrder,
            isActive: true,
            clueText: "Look near the entrance.",
            clueVisibilityPolicy: "VisibleAtStart");
    }

    internal static IReadOnlyCollection<TriviaQuestionSnapshot> CreateQuestions(Guid substageSnapshotId, int count)
    {
        return Enumerable.Range(1, count)
            .Select(sequenceOrder => CreateQuestion(substageSnapshotId, sequenceOrder))
            .ToArray();
    }

    internal static TriviaQuestionSnapshot CreateQuestion(Guid substageSnapshotId, int sequenceOrder = 1)
    {
        return TriviaQuestionSnapshot.Create(
            substageSnapshotId,
            "What is the closest planet to the Sun?",
            sequenceOrder,
            100,
            30,
            "Mercury is the closest planet.",
            CreateOptions());
    }

    internal static IReadOnlyCollection<TriviaOptionSnapshot> CreateOptions()
    {
        return
        [
            TriviaOptionSnapshot.Create("Mercury", 1, true),
            TriviaOptionSnapshot.Create("Venus", 2, false)
        ];
    }

    private static MissionRuntimeSnapshot CreateMixedSnapshot(int maximumTimeMinutes, int triviaQuestionCount)
    {
        var treasureSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1, winnerScore: 100);
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 2);
        var stage = StageSnapshot.Create("Stage One", 1, [treasureSubstage, triviaSubstage]);

        return MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Foundations of Science",
            MaximumTime.Create(maximumTimeMinutes),
            [stage],
            [CreateTarget(treasureSubstage.SubstageSnapshotId)],
            CreateQuestions(triviaSubstage.SubstageSnapshotId, triviaQuestionCount));
    }
}
