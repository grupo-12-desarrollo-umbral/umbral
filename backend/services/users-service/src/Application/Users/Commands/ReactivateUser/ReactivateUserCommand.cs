using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Users.Commands.ReactivateUser;

[Authorize(Roles = "Administrator")]
public sealed record ReactivateUserCommand(int UserId) : IRequest;
