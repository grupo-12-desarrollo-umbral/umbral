using Microsoft.AspNetCore.SignalR;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Rankings;

namespace umbral_backend.Api.Hubs;

public sealed class RankingBroadcaster : IRankingBroadcaster
{
    public const string RankingChangedMethod = "RankingChanged";

    private readonly IHubContext<ScoringHub> _hubContext;

    public RankingBroadcaster(IHubContext<ScoringHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task RankingChanged(Guid liveSessionId, RankingSnapshotDto snapshot, CancellationToken cancellationToken)
    {
        return _hubContext.Clients
            .Group(ScoringHub.BuildSessionGroup(liveSessionId))
            .SendCoreAsync(RankingChangedMethod, [snapshot], cancellationToken);
    }
}
