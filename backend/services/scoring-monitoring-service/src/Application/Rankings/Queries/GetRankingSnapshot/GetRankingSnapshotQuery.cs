namespace umbral_backend.Application.Rankings.Queries.GetRankingSnapshot;

public sealed record GetRankingSnapshotQuery(Guid LiveSessionId, Guid TeamId) : IRequest<RankingSnapshotDto>;
