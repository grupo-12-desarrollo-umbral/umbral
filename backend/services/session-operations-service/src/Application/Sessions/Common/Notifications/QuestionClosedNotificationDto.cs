namespace umbral_backend.Application.Sessions.Common.Notifications;

public sealed record QuestionClosedNotificationDto(
    Guid LiveSessionId,
    int QuestionIndex,
    DateTimeOffset ClosedAt,
    bool WasExpiredByTimer,
    int CorrectOptionSequenceOrder,
    string? Explanation);
