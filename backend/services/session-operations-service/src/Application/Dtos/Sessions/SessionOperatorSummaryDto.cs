namespace umbral_backend.Application.Dtos.Sessions;

public sealed record SessionOperatorSummaryDto(
    Guid LiveSessionId,
    string SessionCode,
    string Title,
    string SessionState,
    int? AssignedOperatorUserId,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? LastTransitionedAt);
