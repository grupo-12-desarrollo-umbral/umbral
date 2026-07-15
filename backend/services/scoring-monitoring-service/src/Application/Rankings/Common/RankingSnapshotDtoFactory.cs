using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Rankings.Common;

internal static class RankingSnapshotDtoFactory
{
    public static RankingSnapshotDto Create(Ranking ranking)
    {
        return new RankingSnapshotDto(
            ranking.LiveSessionId,
            ranking.GeneratedAt,
            ranking.CalculationVersion,
            ranking.Rows
                .Select(row => new RankingRowDto(
                    row.TeamId,
                    row.TeamDisplayName,
                    row.Position,
                    row.TotalScore,
                    row.ResolutionTime.Value))
                .ToArray());
    }
}
