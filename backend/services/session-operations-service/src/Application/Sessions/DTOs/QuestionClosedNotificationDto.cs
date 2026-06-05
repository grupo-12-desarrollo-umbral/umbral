namespace umbral_backend.Application.Sessions.DTOs;

public sealed record QuestionClosedNotificationDto(
    Guid LiveSessionId,
    int QuestionIndex,
    DateTimeOffset ClosedAt,
    bool WasExpiredByTimer);
