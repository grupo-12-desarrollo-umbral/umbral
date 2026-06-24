namespace umbral_backend.Application.Sessions.Common;

public sealed record QuestionClosedNotificationDto(
    Guid LiveSessionId,
    int QuestionIndex,
    DateTimeOffset ClosedAt,
    bool WasExpiredByTimer);
