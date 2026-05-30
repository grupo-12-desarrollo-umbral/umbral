using Microsoft.Extensions.Primitives;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Web.Services;

public class CurrentUser : ICurrentUser
{
    private const string UserIdHeader = "X-User-Id";
    private const string UserRoleHeader = "X-User-Role";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? Id => TryGetHeaderValue(UserIdHeader);

    public List<string>? Roles
    {
        get
        {
            var role = TryGetHeaderValue(UserRoleHeader);
            return string.IsNullOrWhiteSpace(role) ? null : new List<string> { role };
        }
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
