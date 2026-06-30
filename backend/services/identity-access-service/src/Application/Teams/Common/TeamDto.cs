namespace umbral_backend.Application.Teams.Common;

public sealed record TeamDto(
    Guid TeamId,
    string DisplayName,
    string TeamCode,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
