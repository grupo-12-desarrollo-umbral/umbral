using Microsoft.AspNetCore.SignalR;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Api.Hubs;

/// <summary>
/// Evicts a blocked participant (#91) by pushing to their <c>participant:{id}</c> group (the group
/// <see cref="SessionsHub"/> joins each connection to). The client tears its connection down on
/// receipt — the block itself is already authoritative via the synchronous re-check.
/// </summary>
public sealed class ParticipantBlockNotifier : IParticipantBlockNotifier
{
    public const string ParticipationBlockedMethod = "ParticipationBlocked";

    private readonly IHubContext<SessionsHub> _hubContext;

    public ParticipantBlockNotifier(IHubContext<SessionsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyBlockedAsync(Guid sessionParticipantId, CancellationToken cancellationToken)
    {
        return _hubContext.Clients
            .Group($"participant:{sessionParticipantId:D}")
            .SendAsync(ParticipationBlockedMethod, sessionParticipantId, cancellationToken);
    }
}
