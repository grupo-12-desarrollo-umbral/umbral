namespace umbral_backend.Application.Sessions.Common;

public sealed record TeamReferenceDto(
    Guid TeamId,
    string DisplayName,
    string TeamCode,
    bool IsActive,
    int ParticipantCount);
