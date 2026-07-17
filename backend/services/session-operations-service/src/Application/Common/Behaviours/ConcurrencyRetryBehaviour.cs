using Microsoft.Extensions.Logging;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.Common.Behaviours;

/// <summary>
/// Re-runs a handler that lost an optimistic-concurrency race, so a participant never loses a valid
/// action to a collision they cannot see. Retrying the whole handler — rather than re-saving from the
/// repository — is what makes this correct: the handler re-reads the aggregate and re-runs the domain
/// rules against the winner's state, so the retry reaches the same outcome the sequential path would.
/// Two teammates answering at once therefore ends in the ordinary
/// <see cref="DuplicateTriviaAnswerException"/> (409), and two scans of one target end in the
/// ordinary rejected submission, rather than in a 500 from the raw persistence failure.
///
/// Innermost behaviour, so a retried attempt re-runs only the handler, and only the final failure is
/// logged by UnhandledExceptionBehaviour above it.
/// </summary>
public sealed class ConcurrencyRetryBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Small: contention here is between at most a handful of teammates and the timer worker, so a
    // loss that survives three reads is a signal of something other than a transient race.
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
            catch (ConcurrentModificationException exception) when (attempt < MaxAttempts)
            {
                // The failed attempt left its stale aggregate tracked; without this the retry's
                // GetByIdAsync resolves to that same instance and re-decides against the state it
                // already lost on, failing identically every time.
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
