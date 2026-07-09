using umbral_backend.Application.Dtos.Permissions;
using umbral_backend.Application.Dtos.Users;

namespace umbral_backend.Application.Dtos.Users;

public sealed record AuthenticateUserResultDto(
    AuthenticatedActorProfileDto Actor,
    ProtectedAccessDecisionDto Access);
