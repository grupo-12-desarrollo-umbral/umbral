using umbral_backend.Application.Users.Commands.AuthenticateUser;

namespace umbral_backend.Api.Services;

public interface IAuthenticatedUserLoginHandler
{
    Task<AuthenticateUserResultDto> HandleAsync(
        AuthenticatedUserLoginRequest request,
        CancellationToken cancellationToken);
}
