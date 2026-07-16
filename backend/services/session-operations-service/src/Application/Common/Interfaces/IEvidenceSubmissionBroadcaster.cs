using umbral_backend.Application.Sessions.Common.Notifications;

namespace umbral_backend.Application.Common.Interfaces;

/// <summary>
/// Operator-only outbound seam for evidence/submission activity (HU-24B). Kept distinct from
/// <see cref="ISessionQuestionBroadcaster"/> because participants and operators currently share the
/// <c>live-session:{id}</c> group: the Api implementation targets an operator-only group so one team's
/// submissions are never leaked to another team's participant connections. Unlike
/// <see cref="ITeamAnsweredBroadcaster"/> the payload cannot be trimmed to close the leak — a trace row
/// is inherently identifying — so the group boundary this seam enforces is the ONLY gate.
/// </summary>
public interface IEvidenceSubmissionBroadcaster
{
    Task BroadcastEvidenceSubmissionRegisteredAsync(
        EvidenceSubmissionRegisteredNotificationDto notification,
        CancellationToken cancellationToken);

    Task BroadcastEvidenceSubmissionResolvedAsync(
        EvidenceSubmissionResolvedNotificationDto notification,
        CancellationToken cancellationToken);
}
