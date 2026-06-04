using System.IO;
using System.Text.Json;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using umbral_backend.Api.Services;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

public sealed class ProblemDetailsExceptionHandlerTests
{
    private readonly ProblemDetailsExceptionHandler _handler = new();

    [Fact]
    public async Task TryHandleAsync_WithValidationException_ReturnsBadRequestProblemDetails()
    {
        var exception = new ValidationException(
        [
            new ValidationFailure("DisplayName", "Display name is required."),
            new ValidationFailure("DisplayName", "Display name must be shorter.")
        ]);

        var problem = await HandleAsync(exception);

        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Validation failed.");
        problem.Detail.Should().Contain("Display name is required.");
        problem.Detail.Should().Contain("Display name must be shorter.");
    }

    [Fact]
    public async Task TryHandleAsync_WithUnauthorizedAccessException_ReturnsUnauthorizedProblemDetails()
    {
        var problem = await HandleAsync(new UnauthorizedAccessException("Missing trusted headers."));

        problem.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problem.Title.Should().Be("Unauthorized.");
        problem.Detail.Should().Be("Missing trusted headers.");
    }

    [Fact]
    public async Task TryHandleAsync_WithDomainConflictException_ReturnsConflictProblemDetails()
    {
        var problem = await HandleAsync(new TeamCapacityReachedException(Guid.NewGuid(), 4));

        problem.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Conflict.");
    }

    [Fact]
    public async Task TryHandleAsync_WithSourceTriviaQuizNotPublishedException_ReturnsConflictProblemDetails()
    {
        var problem = await HandleAsync(new SourceTriviaQuizNotPublishedException(42, "Draft"));

        problem.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Conflict.");
        problem.Detail.Should().Contain("not published");
    }

    [Fact]
    public async Task TryHandleAsync_WithIneligibleSessionOperatorException_ReturnsBadRequestProblemDetails()
    {
        var problem = await HandleAsync(new IneligibleSessionOperatorException(27));

        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Bad request.");
        problem.Detail.Should().Contain("not eligible");
    }

    [Fact]
    public async Task TryHandleAsync_WithUnknownException_ReturnsInternalServerErrorProblemDetails()
    {
        var problem = await HandleAsync(new InvalidOperationException("boom"));

        problem.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problem.Title.Should().Be("An unexpected error occurred.");
        problem.Detail.Should().Be("boom");
    }

    private async Task<ProblemDetails> HandleAsync(Exception exception)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var handled = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.Body.Position = 0;

        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            httpContext.Response.Body,
            cancellationToken: CancellationToken.None);

        problem.Should().NotBeNull();
        return problem!;
    }
}
