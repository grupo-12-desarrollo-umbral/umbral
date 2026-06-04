namespace umbral_backend.Application.Sessions.DTOs;

public sealed record SessionOperatorSummaryDto(
    Guid LiveSessionId,
    string SessionCode,
    string Title,
    string SessionState,
    int? AssignedOperatorUserId,
    DateTimeOffset ScheduledAt);
