using umbral_backend.Application.Users.Commands.AuthenticateUser;
using umbral_backend.Application.Users.DTOs;

namespace umbral_backend.Api.Services;

public sealed class AuthenticatedUserLoginHandler : IAuthenticatedUserLoginHandler
{
    private readonly ISender _sender;

    public AuthenticatedUserLoginHandler(ISender sender)
    {
        _sender = sender;
    }

    public Task<AuthenticateUserResultDto> HandleAsync(
        AuthenticatedUserLoginRequest request,
        CancellationToken cancellationToken)
    {
        return _sender.Send(
            new AuthenticateUserCommand(
                request.ExternalIdentityId,
                request.DisplayName,
                request.Email,
                request.Role),
            cancellationToken);
    }
}
