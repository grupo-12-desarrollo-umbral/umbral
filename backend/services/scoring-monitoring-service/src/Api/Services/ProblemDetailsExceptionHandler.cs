using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using umbral_backend.Domain.Exceptions;
using ValidationException = umbral_backend.Application.Common.Exceptions.ValidationException;

namespace umbral_backend.Api.Services;

/// <summary>
/// Translates every unhandled exception into an RFC 7807 <see cref="ProblemDetails"/> response.
/// Exceptions classify themselves through <see cref="IErrorMetadata"/>, so this handler never
/// enumerates concrete types. Every <see cref="DomainException"/> is forced to declare an
/// <see cref="ErrorCategory"/> at compile time, so no domain exception can silently fall through
/// to HTTP 500; application-layer exceptions opt in by implementing <see cref="IErrorMetadata"/>.
/// Anything implementing neither hits the HTTP 500 fallback by design: those exceptions are
/// unanticipated, so their messages may carry constraint names, column names or connection strings.
/// The unclassified and unauthorized arms therefore return a generic <c>Detail</c> and correlate to
/// the logged exception through a <c>traceId</c> extension instead of echoing <c>exception.Message</c>.
/// Classified exceptions are equally guarded: their <c>Detail</c> is the exception's curated,
/// identifier-free <see cref="IErrorMetadata.PublicDetail"/> when it opts in, and otherwise a generic
/// per-category sentence — a domain message is never echoed, since it routinely interpolates ids.
/// </summary>
public sealed class ProblemDetailsExceptionHandler(ILogger<ProblemDetailsExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            ValidationException validationException => ValidationProblem(validationException),
            IErrorMetadata metadata => Problem(
                StatusFor(metadata.Category),
                metadata.ErrorCode,
                TitleFor(metadata.Category),
                metadata.PublicDetail ?? DetailFor(metadata.Category)),
            // Thrown deliberately by AuthorizationBehaviour and handler guards, so there is nothing
            // to log — but the message is a framework default, so it is not worth echoing either.
            UnauthorizedAccessException => Correlated(
                Problem(
                    StatusCodes.Status401Unauthorized,
                    "unauthorized",
                    "Unauthorized.",
                    "Unauthorized."),
                TraceId(httpContext)),
            _ => Unhandled(exception, httpContext)
        };

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        // RFC 7807 mandates application/problem+json; WriteAsJsonAsync otherwise forces application/json.
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken);
        return true;
    }

    private ProblemDetails Unhandled(Exception exception, HttpContext httpContext)
    {
        var traceId = TraceId(httpContext);

        // The sole record of this exception: TryHandleAsync always returns true, so
        // ExceptionHandlerMiddleware never logs, and anything thrown outside the MediatR
        // pipeline never reaches UnhandledExceptionBehaviour either.
        logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", traceId);

        return Correlated(
            Problem(
                StatusCodes.Status500InternalServerError,
                "internal-error",
                "An unexpected error occurred.",
                "An unexpected error occurred."),
            traceId);
    }

    // Activity.Id is the whole traceparent (00-<trace>-<span>-01), but log backends index the bare
    // 32-hex trace-id, so emit that: whatever a client quotes must be pasteable into a log search.
    private static string TraceId(HttpContext httpContext) =>
        Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

    private static ProblemDetails Correlated(ProblemDetails problem, string traceId)
    {
        problem.Extensions["traceId"] = traceId;
        return problem;
    }

    private static ProblemDetails Problem(int status, string type, string title, string detail) =>
        new()
        {
            Type = type,
            Title = title,
            Detail = detail,
            Status = status
        };

    private static ProblemDetails ValidationProblem(ValidationException exception)
    {
        var problem = Problem(
            StatusCodes.Status400BadRequest,
            "validation-failed",
            "Validation failed.",
            string.Join(" ", exception.Errors.SelectMany(entry => entry.Value)));

        // Expose the structured FluentValidation failures so clients can map errors to fields
        // instead of parsing the flattened Detail string.
        problem.Extensions["errors"] = exception.Errors;
        return problem;
    }

    private static int StatusFor(ErrorCategory category) => category switch
    {
        ErrorCategory.NotFound => StatusCodes.Status404NotFound,
        ErrorCategory.Validation => StatusCodes.Status400BadRequest,
        ErrorCategory.Conflict => StatusCodes.Status409Conflict,
        ErrorCategory.Forbidden => StatusCodes.Status403Forbidden,
        ErrorCategory.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorCategory.Unprocessable => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status500InternalServerError
    };

    private static string TitleFor(ErrorCategory category) => category switch
    {
        ErrorCategory.NotFound => "Resource not found.",
        ErrorCategory.Validation => "Validation failed.",
        ErrorCategory.Conflict => "Conflict.",
        ErrorCategory.Forbidden => "Forbidden.",
        ErrorCategory.Unauthorized => "Unauthorized.",
        ErrorCategory.Unprocessable => "Unprocessable entity.",
        _ => "An unexpected error occurred."
    };

    // The safe fallback Detail for a classified exception that declares no PublicDetail: generic
    // enough to leak nothing, while the stable ErrorCode (Type) still tells the client what failed.
    private static string DetailFor(ErrorCategory category) => category switch
    {
        ErrorCategory.NotFound => "The requested resource was not found.",
        ErrorCategory.Validation => "The request was invalid.",
        ErrorCategory.Conflict => "The request conflicts with the current state of the resource.",
        ErrorCategory.Forbidden => "You do not have permission to perform this action.",
        ErrorCategory.Unauthorized => "Authentication is required to perform this action.",
        ErrorCategory.Unprocessable => "The request could not be processed.",
        _ => "An unexpected error occurred."
    };
}
