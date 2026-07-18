using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using umbral_backend.Api.Services;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Api.Services;

public sealed class ProblemDetailsExceptionHandlerTests
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
            [ErrorCategory.Unprocessable] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCategory.ServiceUnavailable] = StatusCodes.Status503ServiceUnavailable
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

        var handler = new ProblemDetailsExceptionHandler(NullLogger<ProblemDetailsExceptionHandler>.Instance);
        var httpContext = CreateHttpContext();
        await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);
        var problem = await ReadProblemDetailsAsync(httpContext);

        problem.Status.Should().Be(
            ExpectedStatus[category],
            $"{type.Name} is categorised {category} and must map to that status, never a silent 500");
        problem.Type.Should().NotBeNullOrWhiteSpace(
            $"{type.Name} should carry a stable Type slug");
    }

    [Fact]
    public async Task TryHandleAsync_ForKnownExceptions_ReturnsExpectedStatusCode()
    {
        var handler = new ProblemDetailsExceptionHandler(NullLogger<ProblemDetailsExceptionHandler>.Instance);

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
            new ParticipantAlreadyAuthorizedForTeamException(Guid.NewGuid(), 42),
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
    }

    [Fact]
    public async Task TryHandleAsync_ForValidationException_ReturnsBadRequestWithCombinedDetail()
    {
        var handler = new ProblemDetailsExceptionHandler(NullLogger<ProblemDetailsExceptionHandler>.Instance);
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
    public async Task TryHandleAsync_ForDeactivatedUserRoleAssignment_ReturnsUnprocessableEntity()
    {
        var handler = new ProblemDetailsExceptionHandler(NullLogger<ProblemDetailsExceptionHandler>.Instance);
        var httpContext = CreateHttpContext();
        var exception = new DeactivatedUserRoleAssignmentNotAllowedException(22);

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        var problem = await ReadProblemDetailsAsync(httpContext);
        problem.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problem.Title.Should().Be("Unprocessable entity.");
        problem.Detail.Should().Contain("deactivated");
    }

    [Fact]
    public async Task TryHandleAsync_ForUnknownException_ReturnsInternalServerError()
    {
        var handler = new ProblemDetailsExceptionHandler(NullLogger<ProblemDetailsExceptionHandler>.Instance);
        var httpContext = CreateHttpContext();

        var handled = await handler.TryHandleAsync(httpContext, new InvalidOperationException("boom"), CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        var problem = await ReadProblemDetailsAsync(httpContext);
        problem.Title.Should().Be("An unexpected error occurred.");
    }

    // The unclassified arm catches what nobody anticipated (Npgsql, KeyNotFound, NullReference),
    // whose messages carry constraint names, column names and connection strings.
    private const string SecretMessage = "SECRET-CONNECTION-STRING";

    [Fact]
    public async Task TryHandleAsync_UnclassifiedException_DetailDoesNotEchoMessage()
    {
        var handler = new ProblemDetailsExceptionHandler(NullLogger<ProblemDetailsExceptionHandler>.Instance);
        var httpContext = CreateHttpContext();

        await handler.TryHandleAsync(httpContext, new Exception(SecretMessage), CancellationToken.None);

        var problem = await ReadProblemDetailsAsync(httpContext);
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
        var handler = new ProblemDetailsExceptionHandler(NullLogger<ProblemDetailsExceptionHandler>.Instance);
        var httpContext = CreateHttpContext();

        await handler.TryHandleAsync(httpContext, new Exception(SecretMessage), CancellationToken.None);

        var problem = await ReadProblemDetailsAsync(httpContext);
        problem.Extensions.Should().ContainKey("traceId");
        problem.Extensions["traceId"]!.ToString()
            .Should().Be(activity.TraceId.ToString())
            .And.MatchRegex(
                "^[0-9a-f]{32}$",
                "clients paste the traceId straight into a log search, which indexes the bare trace-id");
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
        // RFC 7807 media type is locked for every handled path, not just fixed.
        httpContext.Response.ContentType.Should().Be("application/problem+json");
        httpContext.Response.Body.Position = 0;
        return (await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body))!;
    }
}
