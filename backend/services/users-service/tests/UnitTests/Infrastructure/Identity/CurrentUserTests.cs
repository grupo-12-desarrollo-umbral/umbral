using Microsoft.AspNetCore.Http;
using umbral_backend.Infrastructure.Identity;

namespace umbral_backend.Application.UnitTests.Infrastructure.Identity;

// Exercises both branches of every null-conditional header read: no HttpContext
// (unauthenticated / background work) → null, and a request carrying the gateway
// identity headers → the header value.
public sealed class CurrentUserTests
{
    private static CurrentUser WithContext(HttpContext? context)
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns(context);
        return new CurrentUser(accessor.Object);
    }

    [Fact]
    public void Members_NoHttpContext_AreNull()
    {
        var currentUser = WithContext(null);

        currentUser.Id.Should().BeNull();
        currentUser.Email.Should().BeNull();
        currentUser.Role.Should().BeNull();
    }

    [Fact]
    public void Members_HeadersPresent_ReturnHeaderValues()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-User-Id"] = "user-123";
        context.Request.Headers["X-User-Email"] = "user@example.com";
        context.Request.Headers["X-User-Role"] = "GameMaster";

        var currentUser = WithContext(context);

        currentUser.Id.Should().Be("user-123");
        currentUser.Email.Should().Be("user@example.com");
        currentUser.Role.Should().Be("GameMaster");
    }
}
