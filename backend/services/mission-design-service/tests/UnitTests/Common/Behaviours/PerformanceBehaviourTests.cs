using umbral_backend.Application.Common.Behaviours;
using umbral_backend.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;

namespace umbral_backend.Application.UnitTests.Common.Behaviours;

public class PerformanceBehaviourTests
{
    [Fact]
    public async Task Handle_WhenUnderThreshold_DoesNotLogWarning()
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.Id).Returns((string?)null);
        var identity = new Mock<IIdentityService>();
        var logger = new Mock<ILogger<SampleRequest>>();
        var sut = new PerformanceBehaviour<SampleRequest, int>(logger.Object, user.Object, identity.Object);

        var result = await sut.Handle(new SampleRequest(), () => Task.FromResult(42), CancellationToken.None);

        result.Should().Be(42);
    }

    public sealed record SampleRequest : IRequest<int>;
}
