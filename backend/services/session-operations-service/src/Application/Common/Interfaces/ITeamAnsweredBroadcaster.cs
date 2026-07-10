using umbral_backend.Application.Sessions.Common.Notifications;

namespace umbral_backend.Application.Common.Interfaces;

/// <summary>
/// Operator-only outbound seam for the "team answered" indicator (HU-34). Kept distinct from
/// <see cref="ISessionQuestionBroadcaster"/> because participants and operators currently share the
/// <c>live-session:{id}</c> group: the Api implementation (X.4) targets an operator-only group so the
/// answered state is never leaked to participant connections. The DTO already omits
/// correctness/points, so the only remaining leak surface is the group boundary this seam enforces.
/// </summary>
public interface ITeamAnsweredBroadcaster
{
    Task BroadcastTeamAnsweredAsync(
        TeamAnsweredNotificationDto notification,
        CancellationToken cancellationToken);
}
