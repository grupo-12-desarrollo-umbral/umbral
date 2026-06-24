using umbral_backend.Application.Users.Commands.AuthenticateUser;

namespace umbral_backend.Api.Services;

public interface IAuthenticatedUserLoginEntryPoint
{
    Task<AuthenticateUserResultDto> AuthenticateAsync(string displayName, CancellationToken cancellationToken);
}
