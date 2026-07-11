using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.SessionOperations.UnitTests.Domain.TestData;

internal static class LiveSessionFactory
{
    internal static LiveSession CreateScheduledTreasureHunt()
    {
        var missionRuntimeSnapshot = MissionRuntimeSnapshotFactory.CreateTreasureHuntSnapshot();

        return LiveSession.Create(
            SessionSource.Create(missionRuntimeSnapshot.SourceMissionId),
            "abc123",
            "Museum Hunt",
            45,
            new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero),
            missionRuntimeSnapshot);
    }

    internal static LiveSession CreateScheduledTrivia(int maximumTimeMinutes = 10)
    {
        var missionRuntimeSnapshot = MissionRuntimeSnapshotFactory.CreateTriviaSnapshot(maximumTimeMinutes);

        return LiveSession.Create(
            SessionSource.Create(missionRuntimeSnapshot.SourceMissionId),
            "tri-123",
            "Trivia Session",
            maximumTimeMinutes,
            new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero),
            missionRuntimeSnapshot);
    }

    internal static LiveSession CreateScheduledTriviaWithThreeQuestions(int maximumTimeMinutes = 10)
    {
        var missionRuntimeSnapshot = MissionRuntimeSnapshotFactory.CreateTriviaSnapshotWithThreeQuestions(maximumTimeMinutes);

        return LiveSession.Create(
            SessionSource.Create(missionRuntimeSnapshot.SourceMissionId),
            "tri-123",
            "Trivia Session",
            maximumTimeMinutes,
            new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero),
            missionRuntimeSnapshot);
    }

    internal static LiveSession CreateScheduledMultiSubstageTrivia(int maximumTimeMinutes = 10)
    {
        var missionRuntimeSnapshot = MissionRuntimeSnapshotFactory.CreateMultiSubstageTriviaSnapshot(maximumTimeMinutes);

        return LiveSession.Create(
            SessionSource.Create(missionRuntimeSnapshot.SourceMissionId),
            "tri-123",
            "Trivia Session",
            maximumTimeMinutes,
            new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero),
            missionRuntimeSnapshot);
    }

    internal static LiveSession CreateScheduledTriviaThenTreasureHunt(int maximumTimeMinutes = 45)
    {
        var missionRuntimeSnapshot = MissionRuntimeSnapshotFactory.CreateTriviaThenTreasureHuntSnapshot(maximumTimeMinutes);

        return LiveSession.Create(
            SessionSource.Create(missionRuntimeSnapshot.SourceMissionId),
            "mix-123",
            "Mixed Session",
            maximumTimeMinutes,
            new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero),
            missionRuntimeSnapshot);
    }

    internal static LiveSession CreateScheduledMultiTargetTreasureHunt(int maximumTimeMinutes = 45)
    {
        var missionRuntimeSnapshot = MissionRuntimeSnapshotFactory.CreateTreasureHuntSnapshotWithMultipleTargets(maximumTimeMinutes);

        return LiveSession.Create(
            SessionSource.Create(missionRuntimeSnapshot.SourceMissionId),
            "th-123",
            "Multi-Target Hunt",
            maximumTimeMinutes,
            new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero),
            missionRuntimeSnapshot);
    }
}
