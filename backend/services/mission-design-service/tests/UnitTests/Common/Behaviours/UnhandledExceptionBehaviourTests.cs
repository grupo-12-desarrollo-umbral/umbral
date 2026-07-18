using umbral_backend.Application.Common.Behaviours;
using umbral_backend.Application.Common.Exceptions;
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

    // Classified 4xx failures and client cancellations are normal control flow, not faults: they must
    // rethrow untouched but never hit the Error sink (which is reserved for genuinely unexpected
    // exceptions, and must not carry the request body). (#7 logging-noise)
    public static IEnumerable<object[]> ExpectedExceptions() => new[]
    {
        new object[] { new ValidationException() },
        new object[] { new ForbiddenAccessException() },
        new object[] { new UnauthorizedAccessException() },
        new object[] { new OperationCanceledException() },
    };

    [Theory]
    [MemberData(nameof(ExpectedExceptions))]
    public async Task Handle_WhenExpectedException_RethrowsWithoutErrorLog(Exception expected)
    {
        var logger = new Mock<ILogger<SampleRequest>>();
        var sut = new UnhandledExceptionBehaviour<SampleRequest, int>(logger.Object);

        var act = () => sut.Handle(new SampleRequest(), () => throw expected, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
        logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    public sealed record SampleRequest : IRequest<int>;
}
