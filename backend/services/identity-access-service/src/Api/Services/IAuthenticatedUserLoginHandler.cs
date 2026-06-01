using umbral_backend.Application.Users.DTOs;

namespace umbral_backend.Api.Services;

public interface IAuthenticatedUserLoginHandler
{
    Task<AuthenticateUserResultDto> HandleAsync(
        AuthenticatedUserLoginRequest request,
        CancellationToken cancellationToken);
}
