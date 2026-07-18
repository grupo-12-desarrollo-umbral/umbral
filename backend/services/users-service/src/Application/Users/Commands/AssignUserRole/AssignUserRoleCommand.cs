using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Users.Commands.AssignUserRole;

[Authorize(Roles = "Administrator")]
public sealed record AssignUserRoleCommand(int UserId, string Role) : IRequest;
