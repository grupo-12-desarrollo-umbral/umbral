using System.Security.Claims;
using Microsoft.Extensions.Primitives;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Api.Services;

public sealed class CurrentUser : ICurrentUser
{
    private const string UserIdHeader = "X-User-Id";
    private const string UserRoleHeader = "X-User-Role";
    private const string UserEmailHeader = "X-User-Email";
    private const string UserNameHeader = "X-User-Name";
    private const string DisplayNameFallback = "Participant";
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CurrentUserContext _userContext;

    public CurrentUser(IHttpContextAccessor httpContextAccessor, CurrentUserContext userContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _userContext = userContext;
    }

    public string? Id => TryGetClaimValue(ClaimTypes.NameIdentifier) ?? TryGetHeaderValue(UserIdHeader);

    public string? Email => TryGetClaimValue(ClaimTypes.Email) ?? TryGetHeaderValue(UserEmailHeader);

    public string? Role => TryGetClaimValue(ClaimTypes.Role) ?? TryGetHeaderValue(UserRoleHeader);

    public string DisplayName =>
        TryGetClaimValue("name")
        ?? TryGetClaimValue(ClaimTypes.Name)
        ?? TryGetHeaderValue(UserNameHeader)
        ?? EmailLocalPart(Email)
        ?? DisplayNameFallback;

    // Last resort before the generic fallback: a user with no name claim still has an email
    // (john.doe@example.com -> john.doe), which beats showing every such participant as "Participant".
    private static string? EmailLocalPart(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var localPart = email.Split('@', 2)[0];
        return string.IsNullOrWhiteSpace(localPart) ? email : localPart;
    }

    private string? TryGetClaimValue(string claimType)
    {
        var principal = _httpContextAccessor.HttpContext?.User ?? _userContext.Principal;
        var value = principal?.FindFirstValue(claimType);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private string? TryGetHeaderValue(string headerName)
    {
        if (_httpContextAccessor.HttpContext?.Request.Headers.TryGetValue(headerName, out StringValues values) != true)
        {
            return null;
        }

        var value = values.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
