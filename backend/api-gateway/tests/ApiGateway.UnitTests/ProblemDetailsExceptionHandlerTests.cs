using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using Xunit;

namespace ApiGateway.UnitTests;

// Guards the gateway's last-resort exception seam: an exception escaping a gateway middleware must
// leave as problem+json carrying a traceId, and must never echo the exception message to the client.
public sealed class ProblemDetailsExceptionHandlerTests
{
    [Fact]
    public async Task EscapedExceptionBecomesProblemJsonWithTraceIdAndNoLeakedMessage()
    {
        const string secret = "Host=db;Password=super-secret-value";
        var handler = new ProblemDetailsExceptionHandler(NullLogger<ProblemDetailsExceptionHandler>.Instance);
        var context = new DefaultHttpContext { TraceIdentifier = "trace-abc" };
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException(secret), default);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().StartWith("application/problem+json");

        var body = await ReadBodyAsync(context);
        body.RootElement.GetProperty("status").GetInt32().Should().Be(500);
        body.RootElement.GetProperty("traceId").GetString().Should().Be("trace-abc");
        body.RootElement.GetProperty("detail").GetString().Should().Be("An unexpected error occurred.");

        var raw = await ReadRawAsync(context);
        raw.Should().NotContain(secret, "the exception message may carry connection strings or host names");
    }

    private static async Task<JsonDocument> ReadBodyAsync(HttpContext context) =>
        JsonDocument.Parse(await ReadRawAsync(context));

    private static async Task<string> ReadRawAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
