using umbral_backend.Application.Sessions.Common;
namespace umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;

public sealed record ReconnectParticipantResultDto(
    Guid LiveSessionId,
    Guid TeamId,
    string TeamDisplayName,
    Guid SessionParticipantId,
    string ParticipantDisplayName,
    string SessionState,
    bool IsReconnect,
    DateTimeOffset JoinedAt,
    DateTimeOffset LastSeenAt,
    SessionTimerSnapshotDto? Timer = null);
