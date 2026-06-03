using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using umbral_backend.Api.Services;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

public sealed class CurrentUserTests
{
    private static readonly CurrentUserContext EmptyContext = new();

    [Fact]
    public void Properties_ReturnTrustedHeaderValues()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = "user-123";
        httpContext.Request.Headers["X-User-Role"] = "Participant";
        httpContext.Request.Headers["X-User-Email"] = "participant@example.com";
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        var currentUser = new CurrentUser(accessor, EmptyContext);

        currentUser.Id.Should().Be("user-123");
        currentUser.Role.Should().Be("Participant");
        currentUser.Email.Should().Be("participant@example.com");
    }

    [Fact]
    public void Properties_ReturnNullWhenHeadersAreMissingOrWhitespace()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = " ";
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        var currentUser = new CurrentUser(accessor, EmptyContext);

        currentUser.Id.Should().BeNull();
        currentUser.Role.Should().BeNull();
        currentUser.Email.Should().BeNull();
    }

    [Fact]
    public void Properties_PreferClaimValuesWhenAvailable()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = "header-user";
        httpContext.Request.Headers["X-User-Role"] = "Operator";
        httpContext.Request.Headers["X-User-Email"] = "header@example.com";
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "claim-user"),
                    new Claim(ClaimTypes.Role, "Participant"),
                    new Claim(ClaimTypes.Email, "claim@example.com")
                ],
                "TrustedHeaders"));
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        var currentUser = new CurrentUser(accessor, EmptyContext);

        currentUser.Id.Should().Be("claim-user");
        currentUser.Role.Should().Be("Participant");
        currentUser.Email.Should().Be("claim@example.com");
    }

    [Fact]
    public void Properties_FallbackToCurrentUserContextWhenHttpContextIsNull()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "hub-user"),
                    new Claim(ClaimTypes.Role, "Participant"),
                    new Claim(ClaimTypes.Email, "hub@example.com")
                ],
                "TrustedHeaders"));
        var context = new CurrentUserContext { Principal = principal };
        var accessor = new HttpContextAccessor { HttpContext = null };

        var currentUser = new CurrentUser(accessor, context);

        currentUser.Id.Should().Be("hub-user");
        currentUser.Role.Should().Be("Participant");
        currentUser.Email.Should().Be("hub@example.com");
    }
}
