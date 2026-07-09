namespace umbral_backend.Application.Dtos.Sessions;

public sealed record AuthenticatedActorProfileLookupDto(
    int UserId,
    string ExternalIdentityId,
    string Role,
    bool IsActive);
