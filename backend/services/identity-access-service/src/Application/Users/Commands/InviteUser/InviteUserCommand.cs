using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Dtos.Users;

namespace umbral_backend.Application.Users.Commands.InviteUser;

[Authorize(Roles = "Administrator")]
public sealed record InviteUserCommand(string Email, string Role) : IRequest<InviteUserResultDto>;
