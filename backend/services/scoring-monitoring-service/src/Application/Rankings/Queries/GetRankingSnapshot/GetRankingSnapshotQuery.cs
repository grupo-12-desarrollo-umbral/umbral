namespace umbral_backend.Application.Rankings.Queries.GetRankingSnapshot;

public sealed record GetRankingSnapshotQuery(Guid LiveSessionId) : IRequest<RankingSnapshotDto>;
