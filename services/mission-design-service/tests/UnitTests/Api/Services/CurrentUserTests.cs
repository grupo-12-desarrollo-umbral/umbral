using Microsoft.AspNetCore.Http;
using umbral_backend.Web.Services;

namespace umbral_backend.Application.UnitTests.Api.Services;

public class CurrentUserTests
{
    [Fact]
    public void IdReadsTheTrustedHeaderValue()
    {
        var currentUser = CreateCurrentUser(("X-User-Id", "user-123"));

        currentUser.Id.Should().Be("user-123");
    }

    [Fact]
    public void RolesReadsTheTrustedHeaderValue()
    {
        var currentUser = CreateCurrentUser(("X-User-Role", "Administrador"));

        currentUser.Roles.Should().BeEquivalentTo(["Administrador"]);
    }

    [Fact]
    public void MissingHeadersReturnNullValues()
    {
        var currentUser = CreateCurrentUser();

        currentUser.Id.Should().BeNull();
        currentUser.Roles.Should().BeNull();
    }

    private static CurrentUser CreateCurrentUser(params (string HeaderName, string HeaderValue)[] headers)
    {
        var httpContext = new DefaultHttpContext();

        foreach (var (headerName, headerValue) in headers)
        {
            httpContext.Request.Headers[headerName] = headerValue;
        }

        return new CurrentUser(new HttpContextAccessor
        {
            HttpContext = httpContext
        });
    }
}
