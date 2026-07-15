using umbral_backend.Application.Dtos.Rankings;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.Dtos.Rankings;

public sealed class RankingSnapshotDtoTests
{
    [Fact]
    public void Empty_ReturnsMinimalPopulatedDto()
    {
        var liveSessionId = Guid.NewGuid();

        var snapshot = RankingSnapshotDto.Empty(liveSessionId);

        snapshot.LiveSessionId.Should().Be(liveSessionId);
        snapshot.GeneratedAt.Should().Be(DateTimeOffset.MinValue);
        snapshot.CalculationVersion.Should().Be(0);
        snapshot.Rows.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_PreservesGeneratedAt()
    {
        var generatedAt = new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

        var snapshot = new RankingSnapshotDto(
            Guid.NewGuid(),
            generatedAt,
            1,
            Array.Empty<RankingRowDto>());

        snapshot.GeneratedAt.Should().Be(generatedAt);
    }
}
