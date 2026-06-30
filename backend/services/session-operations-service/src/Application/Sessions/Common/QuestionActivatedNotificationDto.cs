namespace umbral_backend.Application.Sessions.Common;

public sealed record QuestionActivatedNotificationDto(
    Guid LiveSessionId,
    int QuestionIndex,
    int SequenceOrder,
    string Prompt,
    IReadOnlyList<string> Options,
    int TimeLimitSeconds,
    DateTimeOffset ActivatedAt);
