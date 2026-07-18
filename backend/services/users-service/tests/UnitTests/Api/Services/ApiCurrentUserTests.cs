using Microsoft.AspNetCore.Http;
using umbral_backend.Api.Services;

namespace umbral_backend.Application.UnitTests.Api.Services;

// Covers every branch of TryGetHeaderValue: no HttpContext, a present-but-blank header, and a
// populated header. The Api CurrentUser (distinct from the Infrastructure one) trims blanks to null.
public sealed class ApiCurrentUserTests
{
    private static CurrentUser WithContext(HttpContext? context)
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns(context);
        return new CurrentUser(accessor.Object);
    }

    [Fact]
    public void Id_NoHttpContext_IsNull()
    {
        WithContext(null).Id.Should().BeNull();
    }

    [Fact]
    public void Role_BlankHeader_IsNull()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-User-Role"] = "   ";

        WithContext(context).Role.Should().BeNull();
    }

    [Fact]
    public void Members_HeadersPresent_ReturnTrimmedValues()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-User-Id"] = "u-1";
        context.Request.Headers["X-User-Email"] = "u@example.com";
        context.Request.Headers["X-User-Role"] = "GameMaster";

        var currentUser = WithContext(context);

        currentUser.Id.Should().Be("u-1");
        currentUser.Email.Should().Be("u@example.com");
        currentUser.Role.Should().Be("GameMaster");
    }
}
