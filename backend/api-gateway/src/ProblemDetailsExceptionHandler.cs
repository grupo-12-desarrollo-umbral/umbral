using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway;

/// <summary>
/// Translates any exception that escapes the gateway pipeline — a transform, an auth handler, a
/// misconfigured route — into an RFC 7807 <see cref="ProblemDetails"/> response, so the first hop a
/// client touches never leaks a bare framework 500 with no correlation id.
/// </summary>
/// <remarks>
/// Unlike the three domain services, the gateway owns no exception taxonomy (no MediatR pipeline, no
/// <c>DomainException</c>/<c>IErrorMetadata</c> to classify against), so a fourth copy of their
/// handler could not even compile here — its every arm depends on domain types the gateway does not
/// reference. Everything that reaches this handler is therefore unanticipated: its message may carry
/// a downstream host name, a connection string or a JWKS URL. The handler never echoes
/// <c>exception.Message</c>; it returns a generic HTTP 500 body and correlates to the logged
/// exception through a <c>traceId</c> extension — the one string a client can paste into a log search.
/// </remarks>
public sealed class ProblemDetailsExceptionHandler(ILogger<ProblemDetailsExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = TraceId(httpContext);

        // The sole record of this exception: TryHandleAsync returns true, so ExceptionHandlerMiddleware
        // never logs it, and the gateway has no MediatR pipeline to have caught it upstream.
        logger.LogError(exception, "Unhandled gateway exception. TraceId: {TraceId}", traceId);

        var problemDetails = new ProblemDetails
        {
            Type = "internal-error",
            Title = "An unexpected error occurred.",
            Detail = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError
        };
        problemDetails.Extensions["traceId"] = traceId;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        // RFC 7807 mandates application/problem+json; WriteAsJsonAsync otherwise forces application/json.
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken);
        return true;
    }

    // Activity.Id is the whole traceparent (00-<trace>-<span>-01), but log backends index the bare
    // 32-hex trace-id, so emit that: whatever a client quotes must be pasteable into a log search.
    private static string TraceId(HttpContext httpContext) =>
        Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
}
