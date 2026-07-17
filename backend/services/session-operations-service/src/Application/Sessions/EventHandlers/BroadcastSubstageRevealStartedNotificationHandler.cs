using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Pushes the substage-end ranking reveal (D-3) to the live-session group. Keyed off the domain event
/// rather than off the coordinator, because the two play modes open the reveal from different places:
/// trivia through the coordinator when its last question's answer reveal completes, a treasure hunt
/// straight from <c>RegisterTargetScan</c> on first clear (D-1), which no coordinator sees.
/// </summary>
public sealed class BroadcastSubstageRevealStartedNotificationHandler
    : INotificationHandler<SubstageRevealStartedEvent>
{
    private readonly ISessionQuestionBroadcaster _sessionQuestionBroadcaster;

    public BroadcastSubstageRevealStartedNotificationHandler(
        ISessionQuestionBroadcaster sessionQuestionBroadcaster)
    {
        _sessionQuestionBroadcaster = sessionQuestionBroadcaster;
    }

    public Task Handle(SubstageRevealStartedEvent notification, CancellationToken cancellationToken)
    {
        return _sessionQuestionBroadcaster.BroadcastSubstageRankingRevealStartedAsync(
            new SubstageRankingRevealStartedNotificationDto(
                notification.LiveSessionId,
                notification.SubstageSnapshotId,
                notification.PlayMode.ToString(),
                notification.RevealUntil,
                notification.IsTerminal,
                notification.OccurredAt),
            cancellationToken);
    }
}
