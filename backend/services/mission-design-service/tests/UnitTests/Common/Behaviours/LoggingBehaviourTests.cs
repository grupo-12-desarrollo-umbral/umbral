using umbral_backend.Application.Common.Behaviours;
using umbral_backend.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;

namespace umbral_backend.Application.UnitTests.Common.Behaviours;

public class LoggingBehaviourTests
{
    [Fact]
    public async Task Process_WhenUserIsNull_LogsWithEmptyUserId()
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.Id).Returns((string?)null);
        var identity = new Mock<IIdentityService>();
        var logger = new Mock<ILogger<SampleRequest>>();
        var sut = new LoggingBehaviour<SampleRequest>(logger.Object, user.Object, identity.Object);

        await sut.Process(new SampleRequest(), CancellationToken.None);

        identity.Verify(s => s.GetUserNameAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenUserHasId_LogsWithUserName()
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.Id).Returns("user-1");
        var identity = new Mock<IIdentityService>();
        identity.Setup(s => s.GetUserNameAsync("user-1")).ReturnsAsync("Alice");
        var logger = new Mock<ILogger<SampleRequest>>();
        var sut = new LoggingBehaviour<SampleRequest>(logger.Object, user.Object, identity.Object);

        await sut.Process(new SampleRequest(), CancellationToken.None);

        identity.Verify(s => s.GetUserNameAsync("user-1"), Times.Once);
    }

    public sealed record SampleRequest : IRequest<int>;
}
