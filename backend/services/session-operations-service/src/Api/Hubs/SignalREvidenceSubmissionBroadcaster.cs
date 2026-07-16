using Microsoft.AspNetCore.SignalR;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common.Notifications;

namespace umbral_backend.Api.Hubs;

/// <summary>
/// Operator-only transport for the HU-24B evidence/submission panel. Reuses the shared
/// <see cref="SessionsHub"/> (injected as <see cref="IHubContext{SessionsHub}"/>) but targets the
/// operator-only group — <c>live-session-operators:{id}</c>, built by
/// <see cref="SignalRTeamAnsweredBroadcaster.BuildOperatorGroup"/> rather than re-declared here so the
/// two transports can never drift onto different groups. Participant connections are only ever added to
/// <c>live-session:{id}</c>, <c>team:{id}</c>, and <c>participant:{id}</c>, so they can never receive
/// these signals. A trace row names its team and origin, so unlike the answered indicator the payload
/// cannot be trimmed to make a leak harmless: this group boundary is the whole gate.
/// </summary>
public sealed class SignalREvidenceSubmissionBroadcaster : IEvidenceSubmissionBroadcaster
{
    public const string EvidenceSubmissionRegisteredMethod = "EvidenceSubmissionRegistered";
    public const string EvidenceSubmissionResolvedMethod = "EvidenceSubmissionResolved";

    private readonly IHubContext<SessionsHub> _hubContext;

    public SignalREvidenceSubmissionBroadcaster(IHubContext<SessionsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task BroadcastEvidenceSubmissionRegisteredAsync(
        EvidenceSubmissionRegisteredNotificationDto notification,
        CancellationToken cancellationToken)
    {
        return _hubContext.Clients
            .Group(SignalRTeamAnsweredBroadcaster.BuildOperatorGroup(notification.LiveSessionId))
            .SendCoreAsync(EvidenceSubmissionRegisteredMethod, [notification], cancellationToken);
    }

    public Task BroadcastEvidenceSubmissionResolvedAsync(
        EvidenceSubmissionResolvedNotificationDto notification,
        CancellationToken cancellationToken)
    {
        return _hubContext.Clients
            .Group(SignalRTeamAnsweredBroadcaster.BuildOperatorGroup(notification.LiveSessionId))
            .SendCoreAsync(EvidenceSubmissionResolvedMethod, [notification], cancellationToken);
    }
}
