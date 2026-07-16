namespace umbral_backend.Application.Rankings.Queries.GetOperatorRankingSnapshot;

// Operator-scoped counterpart of GetRankingSnapshotQuery. Carries no TeamId: an operator belongs to no
// team, so access is proven by session assignment rather than team membership.
public sealed record GetOperatorRankingSnapshotQuery(Guid LiveSessionId) : IRequest<RankingSnapshotDto>;
