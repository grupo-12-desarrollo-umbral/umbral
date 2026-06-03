using System.Text.Json;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using umbral_backend.Api.Services;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Api.Services;

public sealed class ProblemDetailsExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ForKnownExceptions_ReturnsExpectedStatusCode()
    {
        var handler = new ProblemDetailsExceptionHandler();

        await AssertHandledAsync(handler, new NotFoundException("User", "kc-01"), StatusCodes.Status404NotFound, "Resource not found.");
        await AssertHandledAsync(handler, new UnauthorizedAccessException("missing headers"), StatusCodes.Status401Unauthorized, "Unauthorized.");
        await AssertHandledAsync(handler, new ForbiddenAccessException(), StatusCodes.Status403Forbidden, "Forbidden.");
        await AssertHandledAsync(
            handler,
            new DeactivatedUserAccessDeniedException(10),
            StatusCodes.Status403Forbidden,
            "Forbidden.");
        await AssertHandledAsync(
            handler,
            new DeactivatedUserRoleAssignmentNotAllowedException(12),
            StatusCodes.Status422UnprocessableEntity,
            "Unprocessable entity.");
        await AssertHandledAsync(
            handler,
            new TeamCodeAlreadyExistsException("RED-01"),
            StatusCodes.Status409Conflict,
            "Conflict.");
        await AssertHandledAsync(
            handler,
            new TeamAlreadyDeactivatedException(Guid.NewGuid()),
            StatusCodes.Status409Conflict,
            "Conflict.");
        await AssertHandledAsync(
            handler,
            new TeamNotActiveException(Guid.NewGuid()),
            StatusCodes.Status409Conflict,
            "Conflict.");
        await AssertHandledAsync(
            handler,
            new ParticipantAlreadyAssignedToTeamException(Guid.NewGuid(), 42),
            StatusCodes.Status409Conflict,
            "Conflict.");
        await AssertHandledAsync(
            handler,
            new ParticipantLockedToAnotherSessionTeamException(42, Guid.NewGuid(), Guid.NewGuid()),
            StatusCodes.Status409Conflict,
            "Conflict.");
        await AssertHandledAsync(
            handler,
            new JoinTokenExpiredException(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow),
            StatusCodes.Status409Conflict,
            "Conflict.");
        await AssertHandledAsync(
            handler,
            new JoinTokenReplayRejectedException(Guid.NewGuid(), global::umbral_backend.Domain.Enums.JoinTokenStatus.Consumed),
            StatusCodes.Status409Conflict,
            "Conflict.");
        await AssertHandledAsync(
            handler,
            new TeamDisplayNameRequiredException(),
            StatusCodes.Status400BadRequest,
            "Validation failed.");
        await AssertHandledAsync(
            handler,
            new UserNotParticipantRoleException(12, global::umbral_backend.Domain.Enums.Role.Operator),
            StatusCodes.Status422UnprocessableEntity,
            "Unprocessable entity.");
        await AssertHandledAsync(
            handler,
            new JoinTokenExpirationInvalidException(),
            StatusCodes.Status400BadRequest,
            "Validation failed.");
    }

    [Fact]
    public async Task TryHandleAsync_ForValidationException_ReturnsBadRequestWithCombinedDetail()
    {
        var handler = new ProblemDetailsExceptionHandler();
        var httpContext = CreateHttpContext();
        var exception = new ValidationException(
        [
            new ValidationFailure("Email", "Email is required."),
            new ValidationFailure("Role", "Role is required.")
        ]);

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var problem = await ReadProblemDetailsAsync(httpContext);
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Validation failed.");
        problem.Detail.Should().Contain("Email is required.");
        problem.Detail.Should().Contain("Role is required.");
    }

    [Fact]
    public async Task TryHandleAsync_ForRoleAssignmentInvariantValidation_ReturnsUnprocessableEntity()
    {
        var handler = new ProblemDetailsExceptionHandler();
        var httpContext = CreateHttpContext();
        var exception = new ValidationException(
        [
            new ValidationFailure("UserId", "Target user must be active.")
        ]);

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        var problem = await ReadProblemDetailsAsync(httpContext);
        problem.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problem.Title.Should().Be("Unprocessable entity.");
        problem.Detail.Should().Contain("Target user must be active.");
    }

    [Fact]
    public async Task TryHandleAsync_ForUnknownException_ReturnsInternalServerError()
    {
        var handler = new ProblemDetailsExceptionHandler();
        var httpContext = CreateHttpContext();

        var handled = await handler.TryHandleAsync(httpContext, new InvalidOperationException("boom"), CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        var problem = await ReadProblemDetailsAsync(httpContext);
        problem.Title.Should().Be("An unexpected error occurred.");
    }

    private static async Task AssertHandledAsync(
        ProblemDetailsExceptionHandler handler,
        Exception exception,
        int expectedStatusCode,
        string expectedTitle)
    {
        var httpContext = CreateHttpContext();

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(expectedStatusCode);

        var problem = await ReadProblemDetailsAsync(httpContext);
        problem.Status.Should().Be(expectedStatusCode);
        problem.Title.Should().Be(expectedTitle);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        return new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            }
        };
    }

    private static async Task<ProblemDetails> ReadProblemDetailsAsync(DefaultHttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        return (await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body))!;
    }
}
