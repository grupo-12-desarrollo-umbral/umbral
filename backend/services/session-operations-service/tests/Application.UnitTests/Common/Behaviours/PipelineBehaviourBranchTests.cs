using umbral_backend.Application.Common.Behaviours;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace umbral_backend.Application.UnitTests.Common.Behaviours;

// Covers the AuthorizationBehaviour arms the command-specific tests skip — a request with no
// [Authorize] attribute, and an [Authorize] carrying no roles (any authenticated caller passes) —
// and PerformanceBehaviour's >500ms slow-request logging branch with and without a current user id.
public sealed class PipelineBehaviourBranchTests
{
    private sealed record PlainRequest;

    [Authorize]
    private sealed record UnrestrictedRequest;

    private static Mock<ICurrentUser> User(string? id, string? role = null)
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.Id).Returns(id);
        user.SetupGet(u => u.Role).Returns(role);
        return user;
    }

    [Fact]
    public async Task Authorization_NoAuthorizeAttribute_AllowsExecution()
    {
        var behaviour = new AuthorizationBehaviour<PlainRequest, string>(User(null).Object);

        var result = await behaviour.Handle(new PlainRequest(), () => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Authorization_AuthorizeWithoutRoles_AllowsAnyAuthenticatedCaller()
    {
        var behaviour = new AuthorizationBehaviour<UnrestrictedRequest, string>(User("42", "AnyRole").Object);

        var result = await behaviour.Handle(new UnrestrictedRequest(), () => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Performance_FastRequest_ReturnsWithoutLogging()
    {
        var behaviour = new PerformanceBehaviour<PlainRequest, string>(
            NullLogger<PlainRequest>.Instance, User("1").Object);

        var result = await behaviour.Handle(new PlainRequest(), () => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Performance_SlowRequestWithUser_Logs()
    {
        var behaviour = new PerformanceBehaviour<PlainRequest, string>(
            NullLogger<PlainRequest>.Instance, User("user-1").Object);

        var result = await behaviour.Handle(
            new PlainRequest(), async () => { await Task.Delay(550); return "ok"; }, CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Performance_SlowRequestWithoutUser_Logs()
    {
        var behaviour = new PerformanceBehaviour<PlainRequest, string>(
            NullLogger<PlainRequest>.Instance, User(null).Object);

        var result = await behaviour.Handle(
            new PlainRequest(), async () => { await Task.Delay(550); return "ok"; }, CancellationToken.None);

        result.Should().Be("ok");
    }
}
