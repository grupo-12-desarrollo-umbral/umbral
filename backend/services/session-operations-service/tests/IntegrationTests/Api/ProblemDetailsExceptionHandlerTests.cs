using System.IO;
using System.Runtime.CompilerServices;
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
    public async Task TryHandleAsync_WithIneligibleSessionOperatorException_ReturnsBadRequestProblemDetails()
    {
        var problem = await HandleAsync(new IneligibleSessionOperatorException(27));

        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Validation failed.");
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

        var problem = await HandleAsync(exception);

        problem.Status.Should().Be(
            ExpectedStatus[category],
            $"{type.Name} is categorised {category} and must map to that status, never a silent 500");
        problem.Type.Should().NotBeNullOrWhiteSpace(
            $"{type.Name} should carry a stable Type slug");
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
