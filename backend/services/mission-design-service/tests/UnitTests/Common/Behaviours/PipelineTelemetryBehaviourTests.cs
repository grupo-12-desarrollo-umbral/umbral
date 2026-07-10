using Moq;
using umbral_backend.Application.Common.Behaviours;
using umbral_backend.Application.Common.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace umbral_backend.Application.UnitTests.Common.Behaviours;

// Covers PerformanceBehaviour (the >500ms slow-request warning path plus the fast no-op path) and
// LoggingBehaviour (the pre-processor), including both sides of the "resolve the user name only
// when a user id is present" branch each shares.
public sealed class PipelineTelemetryBehaviourTests
{
    private sealed record Ping(string Message);

    private static Mock<ICurrentUser> User(string? id)
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.Id).Returns(id);
        return user;
    }

    // ── PerformanceBehaviour ─────────────────────────────────────────────────
    [Fact]
    public async Task Performance_FastRequest_DoesNotResolveUserName()
    {
        var identity = new Mock<IIdentityService>();
        var behaviour = new PerformanceBehaviour<Ping, string>(
            NullLogger<Ping>.Instance, User("user-1").Object, identity.Object);

        var result = await behaviour.Handle(new Ping("hi"), () => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
        identity.Verify(i => i.GetUserNameAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Performance_SlowRequestWithUser_LogsAndResolvesUserName()
    {
        var identity = new Mock<IIdentityService>();
        identity.Setup(i => i.GetUserNameAsync("user-1")).ReturnsAsync("Ada");
        var behaviour = new PerformanceBehaviour<Ping, string>(
            NullLogger<Ping>.Instance, User("user-1").Object, identity.Object);

        var result = await behaviour.Handle(
            new Ping("hi"),
            async () => { await Task.Delay(550); return "ok"; },
            CancellationToken.None);

        result.Should().Be("ok");
        identity.Verify(i => i.GetUserNameAsync("user-1"), Times.Once);
    }

    [Fact]
    public async Task Performance_SlowRequestWithoutUser_SkipsUserNameLookup()
    {
        var identity = new Mock<IIdentityService>();
        var behaviour = new PerformanceBehaviour<Ping, string>(
            NullLogger<Ping>.Instance, User(null).Object, identity.Object);

        await behaviour.Handle(
            new Ping("hi"),
            async () => { await Task.Delay(550); return "ok"; },
            CancellationToken.None);

        identity.Verify(i => i.GetUserNameAsync(It.IsAny<string>()), Times.Never);
    }

    // ── LoggingBehaviour ─────────────────────────────────────────────────────
    [Fact]
    public async Task Logging_WithUser_ResolvesUserName()
    {
        var identity = new Mock<IIdentityService>();
        identity.Setup(i => i.GetUserNameAsync("user-1")).ReturnsAsync("Ada");
        var behaviour = new LoggingBehaviour<Ping>(
            NullLogger<Ping>.Instance, User("user-1").Object, identity.Object);

        await behaviour.Process(new Ping("hi"), CancellationToken.None);

        identity.Verify(i => i.GetUserNameAsync("user-1"), Times.Once);
    }

    [Fact]
    public async Task Logging_WithoutUser_SkipsUserNameLookup()
    {
        var identity = new Mock<IIdentityService>();
        var behaviour = new LoggingBehaviour<Ping>(
            NullLogger<Ping>.Instance, User(null).Object, identity.Object);

        await behaviour.Process(new Ping("hi"), CancellationToken.None);

        identity.Verify(i => i.GetUserNameAsync(It.IsAny<string>()), Times.Never);
    }
}
