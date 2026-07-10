using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common.Notifications;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Bridges the <see cref="AnswerRegisteredEvent"/> domain fact onto the operator-only SignalR signal
/// (HU-34). Maps it to <see cref="TeamAnsweredNotificationDto"/>, which DROPS IsCorrect, ScoreValue,
/// and the selected option — the answered indicator carries no correctness/points. Delivery is
/// routed to the operator-only group by <see cref="ITeamAnsweredBroadcaster"/> (X.4) so participants
/// never receive it.
/// </summary>
public sealed class TeamAnsweredNotificationHandler : INotificationHandler<AnswerRegisteredEvent>
{
    private readonly ITeamAnsweredBroadcaster _broadcaster;

    public TeamAnsweredNotificationHandler(ITeamAnsweredBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    public Task Handle(AnswerRegisteredEvent notification, CancellationToken cancellationToken)
    {
        return _broadcaster.BroadcastTeamAnsweredAsync(
            new TeamAnsweredNotificationDto(
                notification.LiveSessionId,
                notification.TeamId,
                notification.ActiveSubstageId,
                notification.QuestionSequenceOrder,
                notification.SubmittedAt),
            cancellationToken);
    }
}
