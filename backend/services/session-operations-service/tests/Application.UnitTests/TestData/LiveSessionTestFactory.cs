using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.TestData;

internal static class LiveSessionTestFactory
{
    internal static LiveSession CreateScheduledTreasureHunt(
        string sessionCode = "abc123",
        string title = "Museum Hunt",
        int maximumTimeMinutes = 45,
        DateTimeOffset? scheduledAt = null)
    {
        var missionRuntimeSnapshot = CreateTreasureHuntRuntimeSnapshot(maximumTimeMinutes);

        return LiveSession.Create(
            SessionSource.Create(missionRuntimeSnapshot.SourceMissionId),
            sessionCode,
            title,
            maximumTimeMinutes,
            scheduledAt ?? new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero),
            missionRuntimeSnapshot);
    }

    internal static LiveSession CreateScheduledTrivia(
        string sessionCode = "tri-123",
        string title = "Trivia Session",
        int maximumTimeMinutes = 10,
        int questionCount = 1,
        DateTimeOffset? scheduledAt = null)
    {
        var missionRuntimeSnapshot = CreateTriviaRuntimeSnapshot(maximumTimeMinutes, questionCount);

        return LiveSession.Create(
            SessionSource.Create(missionRuntimeSnapshot.SourceMissionId),
            sessionCode,
            title,
            maximumTimeMinutes,
            scheduledAt ?? new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero),
            missionRuntimeSnapshot);
    }

    // Two trivia substages in strict order — the all-trivia multi-substage mission ADR-0005 verifies
    // end-to-end. Closing the first substage's last question advances to the second substage.
    internal static LiveSession CreateScheduledMultiSubstageTrivia(
        string sessionCode = "multi-123",
        string title = "Multi Substage Trivia",
        int maximumTimeMinutes = 45,
        int questionsPerSubstage = 1,
        DateTimeOffset? scheduledAt = null)
    {
        var firstSubstage = SubstageSnapshot.CreateTrivia("Trivia Round One", 1);
        var secondSubstage = SubstageSnapshot.CreateTrivia("Trivia Round Two", 2);
        var missionRuntimeSnapshot = MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Foundations of Science",
            MaximumTime.Create(maximumTimeMinutes),
            [StageSnapshot.Create("Stage One", 1, [firstSubstage, secondSubstage])],
            [],
            [
                .. CreateTriviaQuestions(firstSubstage.SubstageSnapshotId, questionsPerSubstage),
                .. CreateTriviaQuestions(secondSubstage.SubstageSnapshotId, questionsPerSubstage)
            ]);

        return CreateFrom(missionRuntimeSnapshot, sessionCode, title, maximumTimeMinutes, scheduledAt);
    }

    // Trivia substage first, treasure-hunt second — advancing past the trivia substage PARKS at the
    // treasure-hunt substage (D-4); its runtime is downstream (HU-29-32).
    internal static LiveSession CreateScheduledTriviaThenTreasureHunt(
        string sessionCode = "mix-123",
        string title = "Mixed Mission",
        int maximumTimeMinutes = 45,
        DateTimeOffset? scheduledAt = null)
    {
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);
        var treasureSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 2, winnerScore: 100);
        var missionRuntimeSnapshot = MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Mixed Mission",
            MaximumTime.Create(maximumTimeMinutes),
            [StageSnapshot.Create("Stage One", 1, [triviaSubstage, treasureSubstage])],
            [
                TargetSnapshot.Create(
                    treasureSubstage.SubstageSnapshotId,
                    "Main Exhibit",
                    "QR-001",
                    1,
                    isActive: true,
                    clueText: "Look near the entrance.",
                    clueVisibilityPolicy: "VisibleAtStart")
            ],
            CreateTriviaQuestions(triviaSubstage.SubstageSnapshotId, 1));

        return CreateFrom(missionRuntimeSnapshot, sessionCode, title, maximumTimeMinutes, scheduledAt);
    }

    private static LiveSession CreateFrom(
        MissionRuntimeSnapshot missionRuntimeSnapshot,
        string sessionCode,
        string title,
        int maximumTimeMinutes,
        DateTimeOffset? scheduledAt)
    {
        return LiveSession.Create(
            SessionSource.Create(missionRuntimeSnapshot.SourceMissionId),
            sessionCode,
            title,
            maximumTimeMinutes,
            scheduledAt ?? new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero),
            missionRuntimeSnapshot);
    }

    private static TriviaQuestionSnapshot[] CreateTriviaQuestions(Guid substageSnapshotId, int count)
    {
        return Enumerable.Range(1, count)
            .Select(sequenceOrder => TriviaQuestionSnapshot.Create(
                substageSnapshotId,
                sequenceOrder == 1 ? "What is the closest planet to the Sun?" : $"Question {sequenceOrder}",
                sequenceOrder,
                100,
                30,
                sequenceOrder == 1 ? "Mercury is the closest planet." : null,
                [
                    TriviaOptionSnapshot.Create(sequenceOrder == 1 ? "Mercury" : $"Option {sequenceOrder}A", 1, true),
                    TriviaOptionSnapshot.Create(sequenceOrder == 1 ? "Venus" : $"Option {sequenceOrder}B", 2, false)
                ]))
            .ToArray();
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntRuntimeSnapshot(int maximumTimeMinutes)
    {
        var treasureSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1, winnerScore: 100);

        return MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Museum Hunt",
            MaximumTime.Create(maximumTimeMinutes),
            [StageSnapshot.Create("Stage One", 1, [treasureSubstage])],
            [
                TargetSnapshot.Create(
                    treasureSubstage.SubstageSnapshotId,
                    "Main Exhibit",
                    "QR-001",
                    1,
                    isActive: true,
                    clueText: "Look near the entrance.",
                    clueVisibilityPolicy: "VisibleAtStart")
            ],
            []);
    }

    private static MissionRuntimeSnapshot CreateTriviaRuntimeSnapshot(int maximumTimeMinutes, int questionCount)
    {
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);

        return MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Foundations of Science",
            MaximumTime.Create(maximumTimeMinutes),
            [StageSnapshot.Create("Stage One", 1, [triviaSubstage])],
            [],
            Enumerable.Range(1, questionCount)
                .Select(index => TriviaQuestionSnapshot.Create(
                    triviaSubstage.SubstageSnapshotId,
                    index == 1 ? "What is the closest planet to the Sun?" : $"Question {index}",
                    index,
                    100,
                    30,
                    index == 1 ? "Mercury is the closest planet." : null,
                    [
                        TriviaOptionSnapshot.Create(index == 1 ? "Mercury" : $"Option {index}A", 1, true),
                        TriviaOptionSnapshot.Create(index == 1 ? "Venus" : $"Option {index}B", 2, false)
                    ]))
                .ToArray());
    }
}
