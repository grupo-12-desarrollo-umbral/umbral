using umbral_backend.Application.Users.DTOs;

namespace umbral_backend.Api.Services;

public interface IAuthenticatedUserLoginEntryPoint
{
    Task<AuthenticateUserResultDto> AuthenticateAsync(string displayName, CancellationToken cancellationToken);
}
