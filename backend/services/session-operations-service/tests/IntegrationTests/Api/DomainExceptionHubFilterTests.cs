using System.Diagnostics;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using umbral_backend.Api.Hubs;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

/// <summary>
/// The hub filter is the worst of the four leak sites: it catches every exception and serialises the
/// message into a <see cref="HubException"/> delivered to a mobile client. These tests pin that the
/// "ERROR" arm is generic and logged, while the curated codes stay byte-identical to their published
/// contract.
/// </summary>
public sealed class DomainExceptionHubFilterTests
{
    private const string SecretMessage = "SECRET-CONNECTION-STRING";
    private const string ConnectionId = "connection-under-test";

    [Fact]
    public async Task InvokeMethodAsync_UnclassifiedException_MessageDoesNotEchoException()
    {
        var payload = await CapturePayloadAsync(new Exception(SecretMessage));

        payload.GetProperty("code").GetString().Should().Be("ERROR");
        payload.GetProperty("message").GetString().Should().Be("An unexpected error occurred.");
        payload.GetRawText().Should().NotContain(SecretMessage);
    }

    [Fact]
    public async Task InvokeMethodAsync_UnclassifiedException_LogsTheException()
    {
        var logger = new Mock<ILogger<DomainExceptionHubFilter>>();
        var exception = new Exception(SecretMessage);

        await CapturePayloadAsync(exception, logger.Object);

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
    public async Task InvokeMethodAsync_UnclassifiedException_EmitsTraceId()
    {
        using var activity = new Activity(nameof(InvokeMethodAsync_UnclassifiedException_EmitsTraceId)).Start();

        var payload = await CapturePayloadAsync(new Exception(SecretMessage));

        payload.GetProperty("traceId").GetString()
            .Should().Be(activity.TraceId.ToString())
            .And.MatchRegex(
                "^[0-9a-f]{32}$",
                "clients paste the traceId straight into a log search, which indexes the bare trace-id");
    }

    [Fact]
    public async Task InvokeMethodAsync_UnclassifiedException_WithoutActivity_FallsBackToConnectionId()
    {
        // No HttpContext exists inside a hub invocation, so the connection id is the correlator.
        Activity.Current = null;

        var payload = await CapturePayloadAsync(new Exception(SecretMessage));

        payload.GetProperty("traceId").GetString().Should().Be(ConnectionId);
    }

    [Fact]
    public async Task InvokeMethodAsync_CuratedException_KeepsMessageAndOmitsTraceId()
    {
        // The five curated UPPER_SNAKE codes are a published mobile contract: their payloads must
        // serialise exactly as they did before traceId existed, or the reconnect policy breaks.
        var logger = new Mock<ILogger<DomainExceptionHubFilter>>();
        var exception = new LateJoinNotAllowedException(SessionState.Active);

        var payload = await CapturePayloadAsync(exception, logger.Object);

        payload.GetProperty("code").GetString().Should().Be("LATE_JOIN_NOT_ALLOWED");
        payload.GetProperty("message").GetString().Should().Be(exception.Message);
        payload.TryGetProperty("traceId", out _).Should().BeFalse("curated payloads carry no traceId");

        logger.Invocations.Should().BeEmpty("a classified 4xx is not an unhandled exception");
    }

    public static IEnumerable<object[]> CuratedExceptions() =>
        new[]
        {
            new object[] { new ParticipantRemovedFromSessionException(Guid.NewGuid()), "PARTICIPANT_REMOVED" },
            new object[] { new ParticipantAssignedToDifferentTeamException(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), "WRONG_TEAM" },
            new object[] { new ParticipantAlreadyConnectedException(Guid.NewGuid()), "ALREADY_CONNECTED" },
            new object[] { new TeamCapacityReachedException(Guid.NewGuid(), 4), "TEAM_UNAVAILABLE" },
            new object[] { new TeamJoinClosedException(Guid.NewGuid()), "TEAM_UNAVAILABLE" },
        };

    [Theory]
    [MemberData(nameof(CuratedExceptions))]
    public async Task InvokeMethodAsync_CuratedException_MapsToItsContractCode(Exception exception, string expectedCode)
    {
        var payload = await CapturePayloadAsync(exception);

        payload.GetProperty("code").GetString().Should().Be(expectedCode);
        // These curated exceptions declare no PublicDetail, so the generic per-code MessageFor arm
        // supplies an identifier-free message (never the interpolated exception message).
        payload.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        payload.TryGetProperty("traceId", out _).Should().BeFalse();
    }

    public static IEnumerable<object[]> CategoryCodes() =>
        new[]
        {
            new object[] { ErrorCategory.NotFound, "NOT_FOUND" },
            new object[] { ErrorCategory.Validation, "VALIDATION_FAILED" },
            new object[] { ErrorCategory.Conflict, "CONFLICT" },
            new object[] { ErrorCategory.Forbidden, "FORBIDDEN" },
            new object[] { ErrorCategory.Unauthorized, "UNAUTHORIZED" },
            new object[] { ErrorCategory.Unprocessable, "UNPROCESSABLE" },
        };

    [Theory]
    [MemberData(nameof(CategoryCodes))]
    public async Task InvokeMethodAsync_MetadataException_DerivesCodeAndMessageFromCategory(
        ErrorCategory category,
        string expectedCode)
    {
        var payload = await CapturePayloadAsync(new FakeMetadataException(category));

        payload.GetProperty("code").GetString().Should().Be(expectedCode);
        payload.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InvokeMethodAsync_MetadataWithUnknownCategory_CollapsesToError()
    {
        // An out-of-range category exercises the default arm of CodeFor → "ERROR" → the traceId path.
        var payload = await CapturePayloadAsync(new FakeMetadataException((ErrorCategory)999));

        payload.GetProperty("code").GetString().Should().Be("ERROR");
        payload.GetProperty("message").GetString().Should().Be("An unexpected error occurred.");
    }

    [Fact]
    public async Task InvokeMethodAsync_UnauthorizedAccess_MapsToUnauthorized()
    {
        var payload = await CapturePayloadAsync(new UnauthorizedAccessException("no headers"));

        payload.GetProperty("code").GetString().Should().Be("UNAUTHORIZED");
        payload.GetProperty("message").GetString().Should().Be("Authentication is required to perform this action.");
    }

    [Fact]
    public async Task InvokeMethodAsync_HubException_IsRethrownUnwrapped()
    {
        var filter = new DomainExceptionHubFilter(NullLogger<DomainExceptionHubFilter>.Instance);
        var original = new HubException("already a hub error");

        var thrown = await Assert.ThrowsAsync<HubException>(async () =>
            await filter.InvokeMethodAsync(CreateInvocationContext(), _ => throw original));

        thrown.Should().BeSameAs(original);
    }

    [Fact]
    public async Task InvokeMethodAsync_OperationCanceled_IsNotWrapped()
    {
        var filter = new DomainExceptionHubFilter(NullLogger<DomainExceptionHubFilter>.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await filter.InvokeMethodAsync(CreateInvocationContext(), _ => throw new OperationCanceledException()));
    }

    [Fact]
    public async Task InvokeMethodAsync_Success_ReturnsResult()
    {
        var filter = new DomainExceptionHubFilter(NullLogger<DomainExceptionHubFilter>.Instance);

        var result = await filter.InvokeMethodAsync(CreateInvocationContext(), _ => ValueTask.FromResult<object?>("ok"));

        result.Should().Be("ok");
    }

    private sealed class FakeMetadataException(ErrorCategory category) : Exception, IErrorMetadata
    {
        public ErrorCategory Category => category;
        public string ErrorCode => "synthetic-error";
        public string? PublicDetail => null;
    }

    private static async Task<JsonElement> CapturePayloadAsync(
        Exception exception,
        ILogger<DomainExceptionHubFilter>? logger = null)
    {
        var filter = new DomainExceptionHubFilter(logger ?? NullLogger<DomainExceptionHubFilter>.Instance);

        var thrown = await Assert.ThrowsAsync<HubException>(async () =>
            await filter.InvokeMethodAsync(CreateInvocationContext(), _ => throw exception));

        return JsonDocument.Parse(thrown.Message).RootElement.Clone();
    }

    private static HubInvocationContext CreateInvocationContext()
    {
        var hub = new TestHub();
        return new HubInvocationContext(
            new FakeHubCallerContext(),
            new ServiceCollection().BuildServiceProvider(),
            hub,
            typeof(TestHub).GetMethod(nameof(TestHub.Invoke))!,
            Array.Empty<object?>());
    }

    private sealed class TestHub : Hub
    {
        public void Invoke()
        {
        }
    }

    private sealed class FakeHubCallerContext : HubCallerContext
    {
        public override string ConnectionId => DomainExceptionHubFilterTests.ConnectionId;

        public override string? UserIdentifier => null;

        public override ClaimsPrincipal? User => null;

        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

        public override IFeatureCollection Features { get; } = new FeatureCollection();

        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort()
        {
        }
    }
}
