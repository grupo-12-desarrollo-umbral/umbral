using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Domain.Exceptions;
using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using ValidationException = umbral_backend.Application.Common.Exceptions.ValidationException;

namespace umbral_backend.Api.Services;

public sealed class ProblemDetailsExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            NotFoundException or TeamNotFoundException => new ProblemDetails
            {
                Title = "Resource not found.",
                Detail = exception.Message,
                Status = StatusCodes.Status404NotFound
            },
            ValidationException validationException => new ProblemDetails
            {
                Title = "Validation failed.",
                Detail = string.Join(" ", validationException.Errors.SelectMany(entry => entry.Value)),
                Status = StatusCodes.Status400BadRequest
            },
            UnauthorizedAccessException => new ProblemDetails
            {
                Title = "Unauthorized.",
                Detail = exception.Message,
                Status = StatusCodes.Status401Unauthorized
            },
            ForbiddenAccessException or LateJoinNotAllowedException => new ProblemDetails
            {
                Title = "Forbidden.",
                Detail = exception.Message,
                Status = StatusCodes.Status403Forbidden
            },
            TeamCapacityReachedException
                or TeamJoinClosedException
                or ParticipantAlreadyConnectedException
                or ParticipantAssignedToDifferentTeamException
                or ParticipantRemovedFromSessionException => new ProblemDetails
            {
                Title = "Conflict.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            },
            _ => new ProblemDetails
            {
                Title = "An unexpected error occurred.",
                Detail = exception.Message,
                Status = StatusCodes.Status500InternalServerError
            }
        };

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
