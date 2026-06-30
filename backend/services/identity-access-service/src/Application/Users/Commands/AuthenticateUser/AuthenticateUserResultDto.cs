using umbral_backend.Application.Permissions.Queries.CheckProtectedCapabilityAccess;
using umbral_backend.Application.Users.Common;

namespace umbral_backend.Application.Users.Commands.AuthenticateUser;

public sealed record AuthenticateUserResultDto(
    AuthenticatedActorProfileDto Actor,
    ProtectedAccessDecisionDto Access);
