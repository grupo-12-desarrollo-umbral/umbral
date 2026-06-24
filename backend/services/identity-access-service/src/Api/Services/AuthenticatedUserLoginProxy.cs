using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.AuthenticateUser;
using umbral_backend.Application.Common.Security;

namespace umbral_backend.Api.Services;

public sealed class AuthenticatedUserLoginProxy : IAuthenticatedUserLoginEntryPoint
{
    private readonly ICurrentUser _currentUser;
    private readonly IAuthenticatedUserLoginHandler _handler;

    public AuthenticatedUserLoginProxy(
        ICurrentUser currentUser,
        IAuthenticatedUserLoginHandler handler)
    {
        _currentUser = currentUser;
        _handler = handler;
    }

    public Task<AuthenticateUserResultDto> AuthenticateAsync(
        string displayName,
        CancellationToken cancellationToken)
    {
        EnsureTrustedIdentity(_currentUser);

        var role = GatewayRoleParser.Parse(_currentUser.Role!);

        return _handler.HandleAsync(
            new AuthenticatedUserLoginRequest(
                _currentUser.Id!,
                displayName,
                _currentUser.Email!,
                role),
            cancellationToken);
    }

    private static void EnsureTrustedIdentity(ICurrentUser currentUser)
    {
        if (string.IsNullOrWhiteSpace(currentUser.Id) ||
            string.IsNullOrWhiteSpace(currentUser.Email) ||
            string.IsNullOrWhiteSpace(currentUser.Role))
        {
            throw new UnauthorizedAccessException("Trusted gateway identity headers are required.");
        }
    }
}
