using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Users.Commands.DeactivateUser;

[Authorize(Roles = "Administrator")]
public sealed record DeactivateUserCommand(int UserId) : IRequest;
