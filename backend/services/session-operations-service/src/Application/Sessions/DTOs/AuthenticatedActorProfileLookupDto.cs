namespace umbral_backend.Application.Sessions.DTOs;

public sealed record AuthenticatedActorProfileLookupDto(
    int UserId,
    string ExternalIdentityId,
    string Role,
    bool IsActive);
