using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Rankings.Common;

namespace umbral_backend.Application.Rankings.Queries.GetRankingSnapshot;

public sealed class GetRankingSnapshotQueryHandler : IRequestHandler<GetRankingSnapshotQuery, RankingSnapshotDto>
{
    private readonly IRankingRepository _rankingRepository;

    public GetRankingSnapshotQueryHandler(IRankingRepository rankingRepository)
    {
        _rankingRepository = rankingRepository;
    }

    public async Task<RankingSnapshotDto> Handle(GetRankingSnapshotQuery request, CancellationToken cancellationToken)
    {
        var ranking = await _rankingRepository.GetByLiveSessionIdAsync(request.LiveSessionId, cancellationToken);

        return ranking is null
            ? RankingSnapshotDto.Empty(request.LiveSessionId)
            : RankingSnapshotDtoFactory.Create(ranking);
    }
}
