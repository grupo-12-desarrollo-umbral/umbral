using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Rankings.Common;
using umbral_backend.Application.Scores.Common.Authorization;

namespace umbral_backend.Application.Rankings.Queries.GetOperatorRankingSnapshot;

public sealed class GetOperatorRankingSnapshotQueryHandler
    : IRequestHandler<GetOperatorRankingSnapshotQuery, RankingSnapshotDto>
{
    private readonly IRankingRepository _rankingRepository;
    private readonly IScoringSessionAccessResolver _accessResolver;

    public GetOperatorRankingSnapshotQueryHandler(
        IRankingRepository rankingRepository,
        IScoringSessionAccessResolver accessResolver)
    {
        _rankingRepository = rankingRepository;
        _accessResolver = accessResolver;
    }

    public async Task<RankingSnapshotDto> Handle(
        GetOperatorRankingSnapshotQuery request,
        CancellationToken cancellationToken)
    {
        // Same resolver the penalty flow uses: Administrator, or the Operator assigned to THIS session.
        await _accessResolver.EnsureAccessAsync(request.LiveSessionId, cancellationToken);

        // The repository fetch was always session-scoped — only the participant guard was team-scoped.
        var ranking = await _rankingRepository.GetByLiveSessionIdAsync(request.LiveSessionId, cancellationToken);

        return ranking is null
            ? RankingSnapshotDto.Empty(request.LiveSessionId)
            : RankingSnapshotDtoFactory.Create(ranking);
    }
}
