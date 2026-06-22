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
/// Anything implementing neither hits the HTTP 500 fallback by design.
/// </summary>
public sealed class ProblemDetailsExceptionHandler : IExceptionHandler
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
                exception.Message),
            UnauthorizedAccessException => Problem(
                StatusCodes.Status401Unauthorized,
                "unauthorized",
                "Unauthorized.",
                exception.Message),
            _ => Problem(
                StatusCodes.Status500InternalServerError,
                "internal-error",
                "An unexpected error occurred.",
                exception.Message)
        };

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
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
}
