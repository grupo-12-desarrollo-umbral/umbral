using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Rankings.Common;

namespace umbral_backend.Application.Rankings.Queries.GetRankingSnapshot;

public sealed class GetRankingSnapshotQueryHandler : IRequestHandler<GetRankingSnapshotQuery, RankingSnapshotDto>
{
    private readonly IRankingRepository _rankingRepository;
    private readonly IRankingSessionMembershipGuard _guard;

    public GetRankingSnapshotQueryHandler(
        IRankingRepository rankingRepository,
        IRankingSessionMembershipGuard guard)
    {
        _rankingRepository = rankingRepository;
        _guard = guard;
    }

    public async Task<RankingSnapshotDto> Handle(GetRankingSnapshotQuery request, CancellationToken cancellationToken)
    {
        await _guard.EnsureAllowedAsync(request.LiveSessionId, request.TeamId, cancellationToken);

        var ranking = await _rankingRepository.GetByLiveSessionIdAsync(request.LiveSessionId, cancellationToken);

        return ranking is null
            ? RankingSnapshotDto.Empty(request.LiveSessionId)
            : RankingSnapshotDtoFactory.Create(ranking);
    }
}
