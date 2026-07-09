using umbral_backend.Application.Dtos.Users;

namespace umbral_backend.Application.Users.Commands.AuthenticateUser;

public sealed record AuthenticateUserCommand(string DisplayName) : IRequest<AuthenticateUserResultDto>;
