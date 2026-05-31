namespace umbral_backend.Application.Users.DTOs;

public sealed record AuthenticatedActorProfileDto(
    string ExternalIdentityId,
    string DisplayName,
    string Email,
    string Role,
    bool IsActive);
