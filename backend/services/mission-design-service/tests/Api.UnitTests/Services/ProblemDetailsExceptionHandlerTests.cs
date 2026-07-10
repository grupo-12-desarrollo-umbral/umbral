using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using umbral_backend.Api.Services;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using ValidationException = umbral_backend.Application.Common.Exceptions.ValidationException;

namespace umbral_backend.Web.UnitTests.Services;

public class ProblemDetailsExceptionHandlerTests
{
    // The category -> HTTP status contract, restated independently of the handler so the
    // coverage test verifies the mapping rather than trusting it. Every category maps to a
    // 4xx, so a match here is also proof the exception never falls through to a 500.
    private static readonly IReadOnlyDictionary<ErrorCategory, int> ExpectedStatus =
        new Dictionary<ErrorCategory, int>
        {
            [ErrorCategory.NotFound] = StatusCodes.Status404NotFound,
            [ErrorCategory.Validation] = StatusCodes.Status400BadRequest,
            [ErrorCategory.Conflict] = StatusCodes.Status409Conflict,
            [ErrorCategory.Forbidden] = StatusCodes.Status403Forbidden,
            [ErrorCategory.Unauthorized] = StatusCodes.Status401Unauthorized,
            [ErrorCategory.Unprocessable] = StatusCodes.Status422UnprocessableEntity
        };

    // Every concrete DomainException in the Domain assembly, one Theory case each. The key is the
    // readable, serialisable FullName so xUnit enumerates a distinct, named case per exception.
    public static IEnumerable<object[]> DomainExceptionTypes() =>
        typeof(DomainException).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true, IsGenericTypeDefinition: false }
                && typeof(DomainException).IsAssignableFrom(type))
            .OrderBy(type => type.Name)
            .Select(type => new object[] { type.FullName! });

    [Theory]
    [MemberData(nameof(DomainExceptionTypes))]
    public async Task TryHandleAsync_DomainException_MapsToItsCategoryStatusNever500(string typeName)
    {
        var type = typeof(DomainException).Assembly.GetType(typeName, throwOnError: true)!;

        // Bypass the (varied) constructors: Category and ErrorCode are constant/derived, so an
        // uninitialised instance is enough to exercise the handler's classification.
        var exception = (Exception)RuntimeHelpers.GetUninitializedObject(type);
        var category = ((IErrorMetadata)exception).Category;

        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(httpContext, exception);

        problem.Status.Should().Be(
            ExpectedStatus[category],
            $"{type.Name} is categorised {category} and must map to that status, never a silent 500");
        problem.Type.Should().NotBeNullOrWhiteSpace(
            $"{type.Name} should carry a stable Type slug");
    }

    private static HttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    private static async Task<ProblemDetails> InvokeHandlerAndReadProblemDetails(HttpContext httpContext, Exception exception)
    {
        var handler = new ProblemDetailsExceptionHandler(NullLogger<ProblemDetailsExceptionHandler>.Instance);
        httpContext.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature { Error = exception });

        await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // RFC 7807 media type is locked for every handled path, not just fixed.
        httpContext.Response.ContentType.Should().Be("application/problem+json");
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
    public async Task TryHandleAsync_TriviaQuizNotEditableException_Returns409()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(
            httpContext,
            new TriviaQuizNotEditableException(TriviaQuizStatus.Published));

        problem.Status.Should().Be(409);
        problem.Title.Should().Be("Conflict.");
        httpContext.Response.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task TryHandleAsync_TriviaQuizCannotBePublishedInCurrentStateException_Returns409()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(
            httpContext,
            new TriviaQuizCannotBePublishedInCurrentStateException(TriviaQuizStatus.Published));

        problem.Status.Should().Be(409);
        problem.Title.Should().Be("Conflict.");
        httpContext.Response.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task TryHandleAsync_TriviaQuizMustHaveAtLeastOneQuestionToPublishException_Returns409()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(
            httpContext,
            new TriviaQuizMustHaveAtLeastOneQuestionToPublishException());

        problem.Status.Should().Be(409);
        problem.Title.Should().Be("Conflict.");
        httpContext.Response.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task TryHandleAsync_TriviaQuizCannotBeDestructivelyRemovedAfterUsageException_Returns409()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(
            httpContext,
            new TriviaQuizCannotBeDestructivelyRemovedAfterUsageException());

        problem.Status.Should().Be(409);
        problem.Title.Should().Be("Conflict.");
        httpContext.Response.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task TryHandleAsync_TriviaQuizCannotBeRetiredWithoutUsageHistoryException_Returns409()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(
            httpContext,
            new TriviaQuizCannotBeRetiredWithoutUsageHistoryException());

        problem.Status.Should().Be(409);
        problem.Title.Should().Be("Conflict.");
        httpContext.Response.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task TryHandleAsync_TriviaQuizCannotBeArchivedInCurrentStateException_Returns409()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(
            httpContext,
            new TriviaQuizCannotBeArchivedInCurrentStateException(TriviaQuizStatus.Draft));

        problem.Status.Should().Be(409);
        problem.Title.Should().Be("Conflict.");
        httpContext.Response.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task TryHandleAsync_TriviaQuizReferencedByActiveMissionException_Returns409()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(
            httpContext,
            new TriviaQuizReferencedByActiveMissionException(7, ["'Forest Hunt' (#12)"]));

        problem.Status.Should().Be(409);
        problem.Title.Should().Be("Conflict.");
        httpContext.Response.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task TryHandleAsync_TriviaQuestionScoreValueRequiredToPublishException_Returns409()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(
            httpContext,
            new TriviaQuestionScoreValueRequiredToPublishException(1));

        problem.Status.Should().Be(409);
        problem.Title.Should().Be("Conflict.");
        httpContext.Response.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task TryHandleAsync_TriviaQuestionTimeLimitRequiredToPublishException_Returns409()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(
            httpContext,
            new TriviaQuestionTimeLimitRequiredToPublishException(1));

        problem.Status.Should().Be(409);
        problem.Title.Should().Be("Conflict.");
        httpContext.Response.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task TryHandleAsync_TriviaQuestionSequenceOrderMustBeUniqueException_Returns409()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(
            httpContext,
            new TriviaQuestionSequenceOrderMustBeUniqueException(2));

        problem.Status.Should().Be(409);
        problem.Title.Should().Be("Conflict.");
        httpContext.Response.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task TryHandleAsync_TriviaQuestionNotFoundException_Returns404()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(
            httpContext,
            new TriviaQuestionNotFoundException(42));

        problem.Status.Should().Be(404);
        problem.Title.Should().Be("Resource not found.");
        httpContext.Response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task TryHandleAsync_MissionNotReadyForActivationException_Returns409()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(
            httpContext,
            new MissionNotReadyForActivationException(["Mission must have at least one stage."]));

        problem.Status.Should().Be(409);
        problem.Title.Should().Be("Conflict.");
        httpContext.Response.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task TryHandleAsync_MissionNodeNotFoundException_Returns404()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(
            httpContext,
            new MissionNodeNotFoundException(7));

        problem.Status.Should().Be(404);
        problem.Title.Should().Be("Resource not found.");
        httpContext.Response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task TryHandleAsync_TargetNotFoundException_Returns404()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(
            httpContext,
            new TargetNotFoundException(9));

        problem.Status.Should().Be(404);
        problem.Title.Should().Be("Resource not found.");
        httpContext.Response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task TryHandleAsync_MissionAlreadyActiveException_Returns409()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(
            httpContext,
            new MissionAlreadyActiveException());

        problem.Status.Should().Be(409);
        problem.Title.Should().Be("Conflict.");
        httpContext.Response.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task TryHandleAsync_MissionAlreadyDeactivatedException_Returns409()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(
            httpContext,
            new MissionAlreadyDeactivatedException());

        problem.Status.Should().Be(409);
        problem.Title.Should().Be("Conflict.");
        httpContext.Response.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task TryHandleAsync_SubstagePlayModeMismatchException_Returns400()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(
            httpContext,
            new SubstagePlayModeMismatchException(SubstagePlayMode.Trivia, SubstagePlayMode.TreasureHunt));

        problem.Status.Should().Be(400);
        problem.Title.Should().Be("Validation failed.");
        httpContext.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task TryHandleAsync_MissionNameRequiredException_Returns400()
    {
        var httpContext = CreateHttpContext();
        var problem = await InvokeHandlerAndReadProblemDetails(
            httpContext,
            new MissionNameRequiredException());

        problem.Status.Should().Be(400);
        problem.Title.Should().Be("Validation failed.");
        httpContext.Response.StatusCode.Should().Be(400);
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

    // The unclassified arm catches what nobody anticipated (Npgsql, KeyNotFound, NullReference),
    // whose messages carry constraint names, column names and connection strings.
    private const string SecretMessage = "SECRET-CONNECTION-STRING";

    [Fact]
    public async Task TryHandleAsync_UnclassifiedException_DetailDoesNotEchoMessage()
    {
        var httpContext = CreateHttpContext();

        var problem = await InvokeHandlerAndReadProblemDetails(httpContext, new Exception(SecretMessage));

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

        await handler.TryHandleAsync(CreateHttpContext(), exception, CancellationToken.None);

        // The exception instance itself must reach the sink: it is the only record that survives,
        // and asserting on the message alone would pass against a logger that drops the exception.
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
        var httpContext = CreateHttpContext();

        var problem = await InvokeHandlerAndReadProblemDetails(httpContext, new Exception(SecretMessage));

        problem.Extensions.Should().ContainKey("traceId");
        problem.Extensions["traceId"]!.ToString()
            .Should().Be(activity.TraceId.ToString())
            .And.MatchRegex(
                "^[0-9a-f]{32}$",
                "clients paste the traceId straight into a log search, which indexes the bare trace-id");
    }

    private sealed class ExceptionHandlerFeature : IExceptionHandlerFeature
    {
        public Exception Error { get; init; } = null!;
    }
}
