using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;

namespace umbral_backend.Web.Services;

public sealed class ProblemDetailsExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            NotFoundException => new ProblemDetails
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
            ForbiddenAccessException => new ProblemDetails
            {
                Title = "Forbidden.",
                Detail = exception.Message,
                Status = StatusCodes.Status403Forbidden
            },
            TriviaQuizNotEditableException => new ProblemDetails
            {
                Title = "Trivia quiz cannot be edited in its current state.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            },
            TriviaQuizCannotBePublishedInCurrentStateException => new ProblemDetails
            {
                Title = "Trivia quiz cannot be published in its current state.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            },
            TriviaQuizCannotBeArchivedInCurrentStateException => new ProblemDetails
            {
                Title = "Trivia quiz cannot be archived in its current state.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            },
            TriviaQuizReferencedByActiveMissionException => new ProblemDetails
            {
                Title = "Trivia quiz cannot be archived while referenced by an active mission.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            },
            TriviaQuizCannotBeDestructivelyRemovedAfterUsageException => new ProblemDetails
            {
                Title = "Trivia quiz cannot be removed destructively after usage.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            },
            TriviaQuizCannotBeRetiredWithoutUsageHistoryException => new ProblemDetails
            {
                Title = "Trivia quiz cannot be retired without usage history.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            },
            TriviaQuizMustHaveAtLeastOneQuestionToPublishException => new ProblemDetails
            {
                Title = "Trivia quiz is not ready for publication.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            },
            TriviaQuestionScoreValueRequiredToPublishException => new ProblemDetails
            {
                Title = "Trivia quiz is not ready for publication.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            },
            TriviaQuestionTimeLimitRequiredToPublishException => new ProblemDetails
            {
                Title = "Trivia quiz is not ready for publication.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            },
            TriviaQuestionSequenceOrderMustBeUniqueException => new ProblemDetails
            {
                Title = "Question sequence order conflict.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            },
            TriviaQuestionNotFoundException => new ProblemDetails
            {
                Title = "Trivia question not found.",
                Detail = exception.Message,
                Status = StatusCodes.Status404NotFound
            },
            MissionNotReadyForActivationException => new ProblemDetails
            {
                Title = "Validation failed.",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            },
            MissionNodeNotFoundException or TargetNotFoundException => new ProblemDetails
            {
                Title = "Mission resource not found.",
                Detail = exception.Message,
                Status = StatusCodes.Status404NotFound
            },
            MissionAlreadyActiveException or MissionAlreadyDeactivatedException => new ProblemDetails
            {
                Title = "Mission cannot change activation state from its current state.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            },
            SubstagePlayModeMismatchException
                or SubstageRequiresPlayModeException
                or ClueMustBelongToSameSubstageException
                or TargetMayReferenceAtMostOneClueException
                or InvalidMissionNodeChildException
                or MissionNameRequiredException
                or MissionDescriptionRequiredException
                or MissionNodeTitleRequiredException
                or MissionNodeSequenceOrderMustBePositiveException
                or TargetNameRequiredException
                or TargetQrCodeRequiredException
                or TargetSequenceOrderMustBePositiveException
                or ScoreValueExceedsMaximumException
                or ScoreValueMustBePositiveException => new ProblemDetails
                {
                    Title = "Invalid mission authoring operation.",
                    Detail = exception.Message,
                    Status = StatusCodes.Status400BadRequest
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
