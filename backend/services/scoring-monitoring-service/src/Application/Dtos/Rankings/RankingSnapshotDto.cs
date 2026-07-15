namespace umbral_backend.Application.Dtos.Rankings;

public sealed record RankingSnapshotDto(
    Guid LiveSessionId,
    DateTimeOffset GeneratedAt,
    long CalculationVersion,
    IReadOnlyList<RankingRowDto> Rows)
{
    public static RankingSnapshotDto Empty(Guid liveSessionId)
    {
        return new RankingSnapshotDto(liveSessionId, DateTimeOffset.MinValue, 0, Array.Empty<RankingRowDto>());
    }
}

public sealed record RankingRowDto(
    Guid TeamId,
    string TeamDisplayName,
    int Position,
    int TotalScore,
    TimeSpan? ResolutionTime);
