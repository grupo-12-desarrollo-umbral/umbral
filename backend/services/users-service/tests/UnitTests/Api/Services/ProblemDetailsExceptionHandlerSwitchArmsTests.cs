using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using umbral_backend.Api.Services;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Api.Services;

// Drives StatusFor/TitleFor/DetailFor across EVERY ErrorCategory arm — including the ones no
// concrete exception exercises today (e.g. a category whose exceptions all supply a PublicDetail,
// so DetailFor is never reached through them) and the unreachable-by-design default arm. Uses a
// synthetic IErrorMetadata carrier with PublicDetail == null so the handler must fall through to
// the per-category DetailFor sentence.
public sealed class ProblemDetailsExceptionHandlerSwitchArmsTests
{
    private sealed class FakeMetadataException(ErrorCategory category) : Exception, IErrorMetadata
    {
        public ErrorCategory Category => category;
        public string ErrorCode => "synthetic-error";
        public string? PublicDetail => null;
    }

    private static readonly IReadOnlyDictionary<ErrorCategory, (int Status, string Title)> Expected =
        new Dictionary<ErrorCategory, (int, string)>
        {
            [ErrorCategory.NotFound] = (StatusCodes.Status404NotFound, "Resource not found."),
            [ErrorCategory.Validation] = (StatusCodes.Status400BadRequest, "Validation failed."),
            [ErrorCategory.Conflict] = (StatusCodes.Status409Conflict, "Conflict."),
            [ErrorCategory.Forbidden] = (StatusCodes.Status403Forbidden, "Forbidden."),
            [ErrorCategory.Unauthorized] = (StatusCodes.Status401Unauthorized, "Unauthorized."),
            [ErrorCategory.Unprocessable] = (StatusCodes.Status422UnprocessableEntity, "Unprocessable entity."),
            [ErrorCategory.ServiceUnavailable] = (StatusCodes.Status503ServiceUnavailable, "Service unavailable."),
        };

    public static IEnumerable<object[]> AllCategories() =>
        Enum.GetValues<ErrorCategory>().Select(category => new object[] { category });

    [Theory]
    [MemberData(nameof(AllCategories))]
    public async Task TryHandleAsync_MetadataWithoutPublicDetail_UsesPerCategoryStatusTitleAndDetail(
        ErrorCategory category)
    {
        var handler = new ProblemDetailsExceptionHandler(NullLogger<ProblemDetailsExceptionHandler>.Instance);
        var httpContext = CreateHttpContext();

        await handler.TryHandleAsync(httpContext, new FakeMetadataException(category), CancellationToken.None);

        var problem = await ReadProblemDetailsAsync(httpContext);
        var (expectedStatus, expectedTitle) = Expected[category];
        problem.Status.Should().Be(expectedStatus);
        problem.Title.Should().Be(expectedTitle);
        // PublicDetail is null, so the generic per-category DetailFor sentence must be used.
        problem.Detail.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TryHandleAsync_MetadataWithUnknownCategory_FallsBackToInternalServerError()
    {
        var handler = new ProblemDetailsExceptionHandler(NullLogger<ProblemDetailsExceptionHandler>.Instance);
        var httpContext = CreateHttpContext();

        // An out-of-range category exercises the default arm of every switch expression.
        await handler.TryHandleAsync(
            httpContext,
            new FakeMetadataException((ErrorCategory)999),
            CancellationToken.None);

        var problem = await ReadProblemDetailsAsync(httpContext);
        problem.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problem.Title.Should().Be("An unexpected error occurred.");
        problem.Detail.Should().Be("An unexpected error occurred.");
    }

    private static DefaultHttpContext CreateHttpContext() =>
        new() { Response = { Body = new MemoryStream() } };

    private static async Task<ProblemDetails> ReadProblemDetailsAsync(DefaultHttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        return (await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body))!;
    }
}
