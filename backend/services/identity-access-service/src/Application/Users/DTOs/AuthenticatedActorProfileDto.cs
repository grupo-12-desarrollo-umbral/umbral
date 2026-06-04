namespace umbral_backend.Application.Users.DTOs;

public sealed record AuthenticatedActorProfileDto(
    int UserId,
    string ExternalIdentityId,
    string DisplayName,
    string Email,
    string Role,
    bool IsActive);
