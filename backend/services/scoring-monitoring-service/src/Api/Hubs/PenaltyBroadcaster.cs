using Microsoft.AspNetCore.SignalR;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Scores;

namespace umbral_backend.Api.Hubs;

public sealed class PenaltyBroadcaster : IPenaltyBroadcaster
{
    public const string PenaltyAppliedMethod = "PenaltyApplied";

    private readonly IHubContext<ScoringHub> _hubContext;

    public PenaltyBroadcaster(IHubContext<ScoringHub> hubContext)
    {
        _hubContext = hubContext;
    }

    // Targets the same session-wide group as the ranking broadcast (participants and operators alike are
    // members of live-session:{id}); the client filters to its own team by TeamId on the payload.
    public Task PenaltyApplied(
        Guid liveSessionId,
        PenaltyAppliedNotificationDto notification,
        CancellationToken cancellationToken)
    {
        return _hubContext.Clients
            .Group(ScoringHub.BuildSessionGroup(liveSessionId))
            .SendCoreAsync(PenaltyAppliedMethod, [notification], cancellationToken);
    }
}
