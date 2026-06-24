namespace umbral_backend.Application.Users.Common;

public sealed record AuthenticatedActorProfileDto(
    int UserId,
    string ExternalIdentityId,
    string DisplayName,
    string Email,
    string Role,
    bool IsActive);
