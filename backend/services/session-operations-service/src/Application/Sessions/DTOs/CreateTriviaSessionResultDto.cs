namespace umbral_backend.Application.Sessions.DTOs;

public sealed record CreateTriviaSessionResultDto(
    Guid LiveSessionId,
    string SessionCode,
    string Title,
    string SessionState,
    DateTimeOffset ScheduledAt,
    int SourceTriviaQuizId,
    int QuestionCount);
