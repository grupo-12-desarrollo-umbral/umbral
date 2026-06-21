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
