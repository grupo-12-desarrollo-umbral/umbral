using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.SessionOperations.UnitTests.Domain.TestData;

internal static class MissionRuntimeSnapshotFactory
{
    internal static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(int maximumTimeMinutes = 45)
    {
        var treasureSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1);
        var stage = StageSnapshot.Create("Stage One", 1, [treasureSubstage]);

        return MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Museum Hunt",
            MaximumTime.Create(maximumTimeMinutes),
            [stage],
            [CreateTarget(treasureSubstage.SubstageSnapshotId)],
            []);
    }

    // Trivia-first (single trivia substage). Play executes against the ACTIVE substage, so trivia
    // tests must start on a trivia substage — the flat-list world where substage order was
    // irrelevant is gone.
    internal static MissionRuntimeSnapshot CreateTriviaSnapshot(int maximumTimeMinutes = 10)
    {
        return CreateSingleTriviaSubstageSnapshot(maximumTimeMinutes, triviaQuestionCount: 1);
    }

    internal static MissionRuntimeSnapshot CreateTriviaSnapshotWithThreeQuestions(int maximumTimeMinutes = 10)
    {
        return CreateSingleTriviaSubstageSnapshot(maximumTimeMinutes, triviaQuestionCount: 3);
    }

    // Two trivia substages in strict order — the all-trivia multi-substage mission ADR-0005 verifies
    // end-to-end. Advancing the first substage activates the second's first question.
    internal static MissionRuntimeSnapshot CreateMultiSubstageTriviaSnapshot(int maximumTimeMinutes = 10)
    {
        var firstSubstage = SubstageSnapshot.CreateTrivia("Trivia Round One", 1);
        var secondSubstage = SubstageSnapshot.CreateTrivia("Trivia Round Two", 2);
        var stage = StageSnapshot.Create("Stage One", 1, [firstSubstage, secondSubstage]);

        return MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Foundations of Science",
            MaximumTime.Create(maximumTimeMinutes),
            [stage],
            [],
            [
                .. CreateQuestions(firstSubstage.SubstageSnapshotId, 1),
                .. CreateQuestions(secondSubstage.SubstageSnapshotId, 1)
            ]);
    }

    // Trivia substage first, treasure-hunt substage second. Advancing past the trivia substage
    // PARKS at the treasure-hunt substage (D-4) — its runtime is downstream (HU-29-32).
    internal static MissionRuntimeSnapshot CreateTriviaThenTreasureHuntSnapshot(int maximumTimeMinutes = 45)
    {
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);
        var treasureSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 2);
        var stage = StageSnapshot.Create("Stage One", 1, [triviaSubstage, treasureSubstage]);

        return MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Mixed Mission",
            MaximumTime.Create(maximumTimeMinutes),
            [stage],
            [CreateTarget(treasureSubstage.SubstageSnapshotId)],
            CreateQuestions(triviaSubstage.SubstageSnapshotId, 1));
    }

    internal static TargetSnapshot CreateTarget(Guid substageSnapshotId, string qrCode = "QR-001", int sequenceOrder = 1, int? score = 100)
    {
        return TargetSnapshot.Create(
            substageSnapshotId,
            "Main Exhibit",
            qrCode,
            sequenceOrder,
            isActive: true,
            score: score,
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

    private static MissionRuntimeSnapshot CreateSingleTriviaSubstageSnapshot(int maximumTimeMinutes, int triviaQuestionCount)
    {
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);
        var stage = StageSnapshot.Create("Stage One", 1, [triviaSubstage]);

        return MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Foundations of Science",
            MaximumTime.Create(maximumTimeMinutes),
            [stage],
            [],
            CreateQuestions(triviaSubstage.SubstageSnapshotId, triviaQuestionCount));
    }
}
