using umbral_backend.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using ValidationException = umbral_backend.Application.Common.Exceptions.ValidationException;

namespace umbral_backend.Application.Common.Behaviours;

public class UnhandledExceptionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<TRequest> _logger;

    public UnhandledExceptionBehaviour(ILogger<TRequest> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            // Classified failures (validation, forbidden, not-found, conflict, …) are mapped to a 4xx
            // by ProblemDetailsExceptionHandler and cancellations are client aborts — both are normal
            // control flow, not faults. Rethrow without an Error-level log so the sink is not flooded
            // with expected outcomes. (#7 logging-noise)
            throw;
        }
        catch (Exception ex)
        {
            // Only genuinely unexpected exceptions reach here. Log the request name — never the request
            // body ({@Request}), which carries gameplay secrets (QR codes, coordinates, trivia answers). (#7)
            var requestName = typeof(TRequest).Name;

            _logger.LogError(ex, "umbral_backend Request: Unhandled Exception for Request {Name}", requestName);

            throw;
        }
    }

    // An exception is "expected" when it classifies itself to a 4xx or represents a client cancellation.
    // ValidationException and any IErrorMetadata carry an ErrorCategory the API maps to a 4xx;
    // UnauthorizedAccessException is thrown deliberately by the auth guards; OperationCanceledException
    // is a client abort. Everything else is a real fault worth an Error log.
    private static bool IsExpected(Exception exception) =>
        exception is ValidationException
            or IErrorMetadata
            or UnauthorizedAccessException
            or OperationCanceledException;
}
