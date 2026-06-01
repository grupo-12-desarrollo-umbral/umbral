using umbral_backend.Application.Users.DTOs;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Users.Commands.AuthenticateUser;

public sealed record AuthenticateUserCommand(
    string ExternalIdentityId,
    string DisplayName,
    string Email,
    Role Role) : IRequest<AuthenticateUserResultDto>;
