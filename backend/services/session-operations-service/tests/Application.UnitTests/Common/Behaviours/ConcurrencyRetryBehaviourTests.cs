using Microsoft.Extensions.Logging.Abstractions;
using umbral_backend.Application.Common.Behaviours;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Common.Behaviours;

// Covers ConcurrencyRetryBehaviour: a handler that lost an optimistic-concurrency race is re-run
// against fresh state, and tracking is reset first so the retry re-reads instead of re-deciding on the
// stale aggregate it already lost with.
public sealed class ConcurrencyRetryBehaviourTests
{
    private sealed record Request;

    private static ConcurrentModificationException Loss() =>
        new(new InvalidOperationException("23505"));

    private static ConcurrencyRetryBehaviour<Request, string> Build(IUnitOfWork unitOfWork) =>
        new(unitOfWork, NullLogger<Request>.Instance);

    [Fact]
    public async Task Handle_WhenHandlerSucceeds_DoesNotResetTracking()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var behaviour = Build(unitOfWork.Object);

        var result = await behaviour.Handle(new Request(), () => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
        unitOfWork.Verify(work => work.ResetTracking(), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenFirstAttemptLosesRace_RetriesAndReturnsSecondResult()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var behaviour = Build(unitOfWork.Object);
        var attempts = 0;

        var result = await behaviour.Handle(
            new Request(),
            () =>
            {
                attempts++;
                return attempts == 1 ? throw Loss() : Task.FromResult("ok");
            },
            CancellationToken.None);

        result.Should().Be("ok");
        attempts.Should().Be(2);
        unitOfWork.Verify(
            work => work.ResetTracking(),
            Times.Once,
            "the losing attempt's stale aggregate must be dropped before the handler re-reads");
    }

    [Fact]
    public async Task Handle_WhenEveryAttemptLosesRace_ThrowsAfterExhaustingBudget()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var behaviour = Build(unitOfWork.Object);
        var attempts = 0;

        var act = async () => await behaviour.Handle(
            new Request(),
            () =>
            {
                attempts++;
                throw Loss();
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrentModificationException>(
            "a loss that survives the retry budget is reported as a Conflict rather than retried forever");

        attempts.Should().Be(3, "the budget is three attempts, not three retries after the first");
        unitOfWork.Verify(work => work.ResetTracking(), Times.Exactly(2));
    }

    // Only concurrency losses are retryable: re-running a handler that failed for any other reason
    // would double any side effect it had already performed.
    [Fact]
    public async Task Handle_WhenHandlerThrowsOtherException_DoesNotRetry()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var behaviour = Build(unitOfWork.Object);
        var attempts = 0;

        var act = async () => await behaviour.Handle(
            new Request(),
            () =>
            {
                attempts++;
                throw new DuplicateTriviaAnswerException(Guid.NewGuid(), 1);
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateTriviaAnswerException>();

        attempts.Should().Be(1);
        unitOfWork.Verify(work => work.ResetTracking(), Times.Never);
    }
}
