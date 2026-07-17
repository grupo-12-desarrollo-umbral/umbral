using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Common.Interfaces;

public interface ISessionQuestionBroadcaster
{
    Task BroadcastQuestionActivatedAsync(
        QuestionActivatedNotificationDto notification,
        CancellationToken cancellationToken);

    Task BroadcastQuestionClosedAsync(
        QuestionClosedNotificationDto notification,
        CancellationToken cancellationToken);

    Task BroadcastSubstageAdvancedAsync(
        SubstageAdvancedNotificationDto notification,
        CancellationToken cancellationToken);

    Task BroadcastSubstageRankingRevealStartedAsync(
        SubstageRankingRevealStartedNotificationDto notification,
        CancellationToken cancellationToken);
}
