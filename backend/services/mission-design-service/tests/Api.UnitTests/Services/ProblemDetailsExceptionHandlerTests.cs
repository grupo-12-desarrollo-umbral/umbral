using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Web.Services;
using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using ValidationException = umbral_backend.Application.Common.Exceptions.ValidationException;

namespace umbral_backend.Web.UnitTests.Services;

public class ProblemDetailsExceptionHandlerTests
{
    private static HttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    private static async Task<ProblemDetails> InvokeHandlerAndReadProblemDetails(HttpContext httpContext, Exception exception)
    {
        var handler = new ProblemDetailsExceptionHandler();
        httpContext.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature { Error = exception });

        await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body)
            ?? new ProblemDetails();
    }

    [Fact]
    public async Task TryHandleAsync_NotFoundException_Returns404()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(httpContext, new NotFoundException("Mission", 99));

        problem.Status.Should().Be(404);
        problem.Title.Should().Be("Resource not found.");
        httpContext.Response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_Returns400()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(httpContext, new ValidationException());

        problem.Status.Should().Be(400);
        problem.Title.Should().Be("Validation failed.");
        httpContext.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task TryHandleAsync_UnauthorizedAccessException_Returns401()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(httpContext, new UnauthorizedAccessException("not allowed"));

        problem.Status.Should().Be(401);
        problem.Title.Should().Be("Unauthorized.");
        httpContext.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task TryHandleAsync_ForbiddenAccessException_Returns403()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(httpContext, new ForbiddenAccessException());

        problem.Status.Should().Be(403);
        problem.Title.Should().Be("Forbidden.");
        httpContext.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task TryHandleAsync_UnknownException_Returns500()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(httpContext, new InvalidOperationException("boom"));

        problem.Status.Should().Be(500);
        problem.Title.Should().Be("An unexpected error occurred.");
        httpContext.Response.StatusCode.Should().Be(500);
    }

    private sealed class ExceptionHandlerFeature : IExceptionHandlerFeature
    {
        public Exception Error { get; init; } = null!;
    }
}
