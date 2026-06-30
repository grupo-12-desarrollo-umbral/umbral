namespace umbral_backend.Application.Users.Commands.AuthenticateUser;

public sealed record AuthenticateUserCommand(string DisplayName) : IRequest<AuthenticateUserResultDto>;
