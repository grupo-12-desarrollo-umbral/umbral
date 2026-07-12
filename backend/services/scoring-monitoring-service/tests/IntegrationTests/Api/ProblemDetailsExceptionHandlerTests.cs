using System.Diagnostics;
using System.IO;
using System.Text.Json;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using umbral_backend.Api.Services;
using umbral_backend.Application.Common.Exceptions;
using ValidationException = umbral_backend.Application.Common.Exceptions.ValidationException;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

// Drives every non-switch arm of the ProblemDetailsExceptionHandler: the validation projection, the
// unauthorized arm, a classified IErrorMetadata carrier that relies on the DEFAULT interface
// PublicDetail (NotFoundException / ForbiddenAccessException), and the unclassified 500 fallback
// (generic detail, exception logged, traceId emitted, problem+json media type).
public sealed class ProblemDetailsExceptionHandlerTests
{
    private readonly ProblemDetailsExceptionHandler _handler =
        new(NullLogger<ProblemDetailsExceptionHandler>.Instance);

    [Fact]
    public async Task TryHandleAsync_WithValidationException_ReturnsBadRequestProblemDetails()
    {
        var exception = new ValidationException(
        [
            new ValidationFailure("Score", "Score is required."),
            new ValidationFailure("Score", "Score must be positive.")
        ]);

        var problem = await HandleAsync(exception);

        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Validation failed.");
        problem.Detail.Should().Contain("Score is required.");
        problem.Detail.Should().Contain("Score must be positive.");
    }

    [Fact]
    public async Task TryHandleAsync_WithUnauthorizedAccessException_ReturnsUnauthorizedProblemDetails()
    {
        var problem = await HandleAsync(new UnauthorizedAccessException("Missing trusted headers."));

        problem.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problem.Title.Should().Be("Unauthorized.");
        problem.Detail.Should().Be("Unauthorized.");
        problem.Detail.Should().NotContain("Missing trusted headers.");
    }

    [Fact]
    public async Task TryHandleAsync_WithNotFoundException_MapsToNotFoundUsingDefaultPublicDetail()
    {
        var problem = await HandleAsync(new NotFoundException("ScoreEntry", 7));

        problem.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Title.Should().Be("Resource not found.");
        problem.Type.Should().Be("not-found");
        // The default interface PublicDetail is null, so the handler substitutes the per-category
        // sentence — never the identifier-carrying exception message.
        problem.Detail.Should().Be("The requested resource was not found.");
        problem.Detail.Should().NotContain("7");
    }

    [Fact]
    public async Task TryHandleAsync_WithForbiddenAccessException_MapsToForbidden()
    {
        var problem = await HandleAsync(new ForbiddenAccessException());

        problem.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("Forbidden.");
        problem.Type.Should().Be("forbidden-access");
    }

    [Fact]
    public async Task TryHandleAsync_WithUnknownException_ReturnsInternalServerErrorProblemDetails()
    {
        var problem = await HandleAsync(new InvalidOperationException("boom"));

        problem.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problem.Title.Should().Be("An unexpected error occurred.");
        problem.Detail.Should().Be("An unexpected error occurred.");
        problem.Detail.Should().NotContain("boom");
    }

    private const string SecretMessage = "SECRET-CONNECTION-STRING";

    [Fact]
    public async Task TryHandleAsync_UnclassifiedException_DetailDoesNotEchoMessage()
    {
        var problem = await HandleAsync(new Exception(SecretMessage));

        problem.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problem.Detail.Should().Be("An unexpected error occurred.");
        problem.Detail.Should().NotContain(SecretMessage);
    }

    [Fact]
    public async Task TryHandleAsync_UnclassifiedException_LogsTheException()
    {
        var logger = new Mock<ILogger<ProblemDetailsExceptionHandler>>();
        var handler = new ProblemDetailsExceptionHandler(logger.Object);
        var exception = new Exception(SecretMessage);
        var httpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        logger.Verify(
            instance => instance.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_UnclassifiedException_EmitsTraceId()
    {
        using var activity = new Activity(nameof(TryHandleAsync_UnclassifiedException_EmitsTraceId)).Start();

        var problem = await HandleAsync(new Exception(SecretMessage));

        problem.Extensions.Should().ContainKey("traceId");
        problem.Extensions["traceId"]!.ToString()
            .Should().Be(activity.TraceId.ToString())
            .And.MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task TryHandleAsync_NoActivity_FallsBackToTraceIdentifier()
    {
        // With no ambient Activity the handler correlates on HttpContext.TraceIdentifier instead.
        var httpContext = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() },
            TraceIdentifier = "trace-identifier-fallback"
        };

        await _handler.TryHandleAsync(httpContext, new Exception(SecretMessage), CancellationToken.None);

        var problem = await ReadAsync(httpContext);
        problem.Extensions["traceId"]!.ToString().Should().Be("trace-identifier-fallback");
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_KeepsBodyShapeUnderProblemJsonMediaType()
    {
        var exception = new ValidationException(
        [
            new ValidationFailure("Score", "Score is required.")
        ]);

        var httpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        httpContext.Response.ContentType.Should().Be("application/problem+json");

        httpContext.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(httpContext.Response.Body);
        var root = document.RootElement;

        root.GetProperty("type").GetString().Should().Be("validation-failed");
        root.GetProperty("title").GetString().Should().Be("Validation failed.");
        root.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status400BadRequest);
        root.GetProperty("detail").GetString().Should().Contain("Score is required.");
        root.GetProperty("errors").GetProperty("Score")
            .EnumerateArray().Select(entry => entry.GetString())
            .Should().Contain("Score is required.");
    }

    private async Task<ProblemDetails> HandleAsync(Exception exception)
    {
        var httpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var handled = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.ContentType.Should().Be("application/problem+json");
        return await ReadAsync(httpContext);
    }

    private static async Task<ProblemDetails> ReadAsync(DefaultHttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            httpContext.Response.Body,
            cancellationToken: CancellationToken.None);
        problem.Should().NotBeNull();
        return problem!;
    }
}
