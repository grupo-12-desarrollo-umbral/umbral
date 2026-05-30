using umbral_backend.Application.Users.DTOs;

namespace umbral_backend.Application.Users.Commands.AuthenticateUser;

public sealed record AuthenticateUserCommand(
    string ExternalIdentityId,
    string DisplayName,
    string Email,
    string Role) : IRequest<AuthenticateUserResultDto>;
