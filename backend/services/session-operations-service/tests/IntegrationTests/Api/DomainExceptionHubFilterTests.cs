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
