using System.Reflection;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Common.Behaviours;

public sealed class AuthorizationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUser _currentUser;

    public AuthorizationBehaviour(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var authorizeAttributes = request.GetType().GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToArray();

        if (authorizeAttributes.Length == 0)
        {
            return next();
        }

        if (string.IsNullOrWhiteSpace(_currentUser.Id))
        {
            throw new UnauthorizedAccessException();
        }

        var roles = authorizeAttributes
            .SelectMany(attribute => attribute.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();

        if (roles.Length > 0 && !roles.Contains(_currentUser.Role, StringComparer.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException();
        }

        return next();
    }
}
