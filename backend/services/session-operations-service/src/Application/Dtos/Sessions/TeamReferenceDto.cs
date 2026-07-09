namespace umbral_backend.Application.Dtos.Sessions;

public sealed record TeamReferenceDto(
    Guid TeamId,
    string DisplayName,
    string TeamCode,
    bool IsActive,
    int ParticipantCount);
