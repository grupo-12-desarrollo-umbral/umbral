using Microsoft.AspNetCore.Http;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Infrastructure.Identity;

internal sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? Id => _httpContextAccessor.HttpContext?.Request.Headers["X-User-Id"];

    public string? Email => _httpContextAccessor.HttpContext?.Request.Headers["X-User-Email"];

    public string? Role => _httpContextAccessor.HttpContext?.Request.Headers["X-User-Role"];
}
