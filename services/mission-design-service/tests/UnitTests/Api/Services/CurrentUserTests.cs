using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using Shouldly;
using umbral_backend.Web.Services;

namespace umbral_backend.Application.UnitTests.Api.Services;

public class CurrentUserTests
{
    [Test]
    public void IdReadsTheTrustedHeaderValue()
    {
        var currentUser = CreateCurrentUser(("X-User-Id", "user-123"));

        currentUser.Id.ShouldBe("user-123");
    }

    [Test]
    public void RolesReadsTheTrustedHeaderValue()
    {
        var currentUser = CreateCurrentUser(("X-User-Role", "Administrador"));

        currentUser.Roles.ShouldBe(["Administrador"]);
    }

    [Test]
    public void MissingHeadersReturnNullValues()
    {
        var currentUser = CreateCurrentUser();

        currentUser.Id.ShouldBeNull();
        currentUser.Roles.ShouldBeNull();
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
