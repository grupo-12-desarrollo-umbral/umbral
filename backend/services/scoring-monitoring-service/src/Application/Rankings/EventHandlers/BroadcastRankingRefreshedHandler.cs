using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Rankings.Common;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Rankings.EventHandlers;

public sealed class BroadcastRankingRefreshedHandler : INotificationHandler<RankingRefreshed>
{
    private readonly IRankingRepository _rankingRepository;
    private readonly IRankingBroadcaster _rankingBroadcaster;

    public BroadcastRankingRefreshedHandler(
        IRankingRepository rankingRepository,
        IRankingBroadcaster rankingBroadcaster)
    {
        _rankingRepository = rankingRepository;
        _rankingBroadcaster = rankingBroadcaster;
    }

    public async Task Handle(RankingRefreshed notification, CancellationToken cancellationToken)
    {
        var ranking = await _rankingRepository.GetByLiveSessionIdAsync(notification.LiveSessionId, cancellationToken);

        if (ranking is null)
        {
            return;
        }

        await _rankingBroadcaster.RankingChanged(
            notification.LiveSessionId,
            RankingSnapshotDtoFactory.Create(ranking),
            cancellationToken);
    }
}
