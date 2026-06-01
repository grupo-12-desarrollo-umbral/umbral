using umbral_backend.Domain.Enums;

namespace umbral_backend.Api.Services;

public sealed record AuthenticatedUserLoginRequest(
    string ExternalIdentityId,
    string DisplayName,
    string Email,
    Role Role);
