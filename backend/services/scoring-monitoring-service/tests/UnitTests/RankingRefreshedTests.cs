using umbral_backend.Domain.Events;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class RankingRefreshedTests
{
    [Fact]
    public void Constructor_ShouldExposeEventPayload()
    {
        var rankingId = Guid.NewGuid();
        var liveSessionId = Guid.NewGuid();
        var generatedAt = new DateTimeOffset(2026, 7, 14, 13, 15, 0, TimeSpan.Zero);

        var @event = new RankingRefreshed(rankingId, liveSessionId, generatedAt, calculationVersion: 3);

        @event.RankingId.Should().Be(rankingId);
        @event.LiveSessionId.Should().Be(liveSessionId);
        @event.GeneratedAt.Should().Be(generatedAt);
        @event.CalculationVersion.Should().Be(3);
    }
}
