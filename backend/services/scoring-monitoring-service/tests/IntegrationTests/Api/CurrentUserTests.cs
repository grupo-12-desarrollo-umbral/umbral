using System.Security.Claims;
using Microsoft.AspNetCore.Http;
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
        httpContext.Request.Headers["X-User-Role"] = "Operator";
        httpContext.Request.Headers["X-User-Email"] = "operator@example.com";
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        var currentUser = new CurrentUser(accessor, EmptyContext);

        currentUser.Id.Should().Be("user-123");
        currentUser.Role.Should().Be("Operator");
        currentUser.Email.Should().Be("operator@example.com");
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
                    new Claim(ClaimTypes.Role, "Administrator"),
                    new Claim(ClaimTypes.Email, "claim@example.com")
                ],
                "TrustedHeaders"));
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        var currentUser = new CurrentUser(accessor, EmptyContext);

        currentUser.Id.Should().Be("claim-user");
        currentUser.Role.Should().Be("Administrator");
        currentUser.Email.Should().Be("claim@example.com");
    }

    [Fact]
    public void Properties_FallbackToCurrentUserContextWhenHttpContextIsNull()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "context-user"),
                    new Claim(ClaimTypes.Role, "Operator"),
                    new Claim(ClaimTypes.Email, "context@example.com")
                ],
                "TrustedHeaders"));
        var context = new CurrentUserContext { Principal = principal };
        var accessor = new HttpContextAccessor { HttpContext = null };

        var currentUser = new CurrentUser(accessor, context);

        currentUser.Id.Should().Be("context-user");
        currentUser.Role.Should().Be("Operator");
        currentUser.Email.Should().Be("context@example.com");
    }

    [Fact]
    public void Properties_ReturnNullWhenHttpContextIsNullAndContextHasNoPrincipal()
    {
        var accessor = new HttpContextAccessor { HttpContext = null };
        var currentUser = new CurrentUser(accessor, EmptyContext);

        currentUser.Id.Should().BeNull();
        currentUser.Role.Should().BeNull();
        currentUser.Email.Should().BeNull();
    }
}
