using Microsoft.Extensions.Logging;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.Common.Behaviours;

/// <summary>
/// Re-runs a handler that lost an optimistic-concurrency race on the ranking projection, so a score
/// event never leaves the ranking stale because a concurrent recalculation won the save. Retrying the
/// whole handler — rather than re-saving from the repository — is what makes this correct: the handler
/// re-reads every committed score entry and the current ranking, then derives the next
/// <c>CalculationVersion</c> from the winner's freshly loaded row. Two score events landing at once
/// therefore fold into one complete ranking rather than one overwriting the other.
///
/// This is Scoring Monitoring's own retry behaviour, deliberately not a reference to the Session
/// Operations one: the two bounded contexts must not share implementation types, so each catches its
/// own concurrency exception (<see cref="ConcurrentRankingModificationException"/> here).
///
/// Innermost behaviour, so a retried attempt re-runs only the handler, and only the final failure is
/// logged by UnhandledExceptionBehaviour above it.
/// </summary>
public sealed class ConcurrencyRetryBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Small: contention here is between the handful of score events a session emits at once, so a loss
    // that survives three reads is a signal of something other than a transient race.
    private const int MaxAttempts = 3;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TRequest> _logger;

    public ConcurrencyRetryBehaviour(IUnitOfWork unitOfWork, ILogger<TRequest> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await next();
            }
            catch (ConcurrentRankingModificationException exception) when (attempt < MaxAttempts)
            {
                // The failed attempt left its stale ranking and score entries tracked; without this the
                // retry's GetByLiveSessionIdAsync resolves to that same instance and recomputes against
                // the state it already lost on, deriving the same losing CalculationVersion every time.
                _unitOfWork.ResetTracking();

                _logger.LogWarning(
                    exception,
                    "Concurrency loss on {RequestName}, attempt {Attempt} of {MaxAttempts}. Retrying.",
                    typeof(TRequest).Name,
                    attempt,
                    MaxAttempts);
            }
        }
    }
}
