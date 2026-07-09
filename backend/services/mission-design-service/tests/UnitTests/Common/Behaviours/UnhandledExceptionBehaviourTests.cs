using umbral_backend.Application.Common.Behaviours;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;

namespace umbral_backend.Application.UnitTests.Common.Behaviours;

/// ProblemDetailsExceptionHandler claims every exception (returns true), so
/// ExceptionHandlerMiddleware never logs it. This behaviour is the only thing
/// that records an unhandled exception — assert it does, and still rethrows.
public class UnhandledExceptionBehaviourTests
{
    [Fact]
    public async Task Handle_WhenException_LogsErrorAndRethrows()
    {
        var logger = new Mock<ILogger<SampleRequest>>();
        var sut = new UnhandledExceptionBehaviour<SampleRequest, int>(logger.Object);
        var exception = new InvalidOperationException("test failure");

        var act = () => sut.Handle(new SampleRequest(), () => throw exception, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    public sealed record SampleRequest : IRequest<int>;
}
