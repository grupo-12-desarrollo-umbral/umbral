using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.SessionOperations.UnitTests.Domain.TestData;

internal static class LiveSessionFactory
{
    internal static LiveSession CreateScheduledTreasureHunt()
    {
        return LiveSession.Create(
            SessionMode.TreasureHunt,
            SessionSource.Create(SessionSourceType.Mission, Guid.NewGuid()),
            "abc123",
            "Museum Hunt",
            45,
            new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero));
    }

    internal static LiveSession CreateScheduledTrivia(int maximumTimeMinutes = 10)
    {
        return LiveSession.CreateTrivia(
            SessionSource.CreateTriviaQuiz(42),
            "tri-123",
            "Trivia Session",
            maximumTimeMinutes,
            new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero),
            TriviaSessionSnapshotFactory.CreateSingleQuestion());
    }
}
