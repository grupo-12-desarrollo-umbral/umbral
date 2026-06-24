using umbral_backend.Application.Permissions.Queries.CheckProtectedCapabilityAccess;

namespace umbral_backend.Application.Users.DTOs;

public sealed record AuthenticateUserResultDto(
    AuthenticatedActorProfileDto Actor,
    ProtectedAccessDecisionDto Access);
