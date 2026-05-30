using umbral_backend.Application.Permissions.DTOs;

namespace umbral_backend.Application.Users.DTOs;

public sealed record AuthenticateUserResultDto(
    AuthenticatedActorProfileDto Actor,
    ProtectedAccessDecisionDto Access);
