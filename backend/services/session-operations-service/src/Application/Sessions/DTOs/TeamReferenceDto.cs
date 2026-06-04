namespace umbral_backend.Application.Sessions.DTOs;

public sealed record TeamReferenceDto(
    Guid TeamId,
    string DisplayName,
    string TeamCode,
    bool IsActive,
    int ParticipantCount);
