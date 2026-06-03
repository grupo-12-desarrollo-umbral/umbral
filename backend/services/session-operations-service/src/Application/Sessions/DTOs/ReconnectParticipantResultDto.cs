namespace umbral_backend.Application.Sessions.DTOs;

public sealed record ReconnectParticipantResultDto(
    Guid LiveSessionId,
    Guid TeamId,
    string TeamDisplayName,
    Guid SessionParticipantId,
    string ParticipantDisplayName,
    string SessionState,
    bool IsReconnect,
    DateTimeOffset JoinedAt,
    DateTimeOffset LastSeenAt);
