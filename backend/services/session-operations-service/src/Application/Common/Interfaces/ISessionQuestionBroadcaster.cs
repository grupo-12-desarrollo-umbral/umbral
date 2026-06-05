using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Common.Interfaces;

public interface ISessionQuestionBroadcaster
{
    Task BroadcastQuestionActivatedAsync(
        QuestionActivatedNotificationDto notification,
        CancellationToken cancellationToken);

    Task BroadcastQuestionClosedAsync(
        QuestionClosedNotificationDto notification,
        CancellationToken cancellationToken);
}
