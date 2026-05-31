using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Domain.Exceptions;
using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;

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
            NotFoundException => new ProblemDetails
            {
                Title = "Resource not found.",
                Detail = exception.Message,
                Status = StatusCodes.Status404NotFound
            },
            ValidationException validationException when IsUnprocessableRoleAssignment(validationException) => new ProblemDetails
            {
                Title = "Unprocessable entity.",
                Detail = string.Join(" ", validationException.Errors.SelectMany(entry => entry.Value)),
                Status = StatusCodes.Status422UnprocessableEntity
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
            ForbiddenAccessException or DeactivatedUserAccessDeniedException or UserRoleNotAuthorizedException => new ProblemDetails
            {
                Title = "Forbidden.",
                Detail = exception.Message,
                Status = StatusCodes.Status403Forbidden
            },
            TeamCodeAlreadyExistsException
                or TeamAlreadyDeactivatedException
                or TeamNotActiveException
                or ParticipantAlreadyAssignedToTeamException => new ProblemDetails
            {
                Title = "Conflict.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            },
            TeamDisplayNameRequiredException or TeamCodeRequiredException => new ProblemDetails
            {
                Title = "Validation failed.",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            },
            DeactivatedUserRoleAssignmentNotAllowedException or UserNotParticipantRoleException => new ProblemDetails
            {
                Title = "Unprocessable entity.",
                Detail = exception.Message,
                Status = StatusCodes.Status422UnprocessableEntity
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

    private static bool IsUnprocessableRoleAssignment(ValidationException validationException)
    {
        return validationException.Errors
            .SelectMany(entry => entry.Value)
            .Any(message => string.Equals(message, "Target user must be active.", StringComparison.Ordinal));
    }
}
