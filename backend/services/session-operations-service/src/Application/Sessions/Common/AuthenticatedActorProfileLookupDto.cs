namespace umbral_backend.Application.Sessions.Common;

public sealed record AuthenticatedActorProfileLookupDto(
    int UserId,
    string ExternalIdentityId,
    string Role,
    bool IsActive);
