using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Forwarder;
using FluentAssertions;
using Xunit;

namespace ApiGateway.UnitTests;

// Guards the YARP proxy-error seam: a forwarder failure must leave as problem+json with the right
// status (unreachable -> 502, timeout -> 504), while a downstream response passes through untouched.
public sealed class ForwarderErrorProblemDetailsMiddlewareTests
{
    [Fact]
    public async Task UnreachableDestinationBecomes502ProblemJson()
    {
        var context = await RunAsync(terminal =>
        {
            terminal.Features.Set<IForwarderErrorFeature>(new StubForwarderError(ForwarderError.Request));
            terminal.Response.StatusCode = StatusCodes.Status502BadGateway;
            return Task.CompletedTask;
        });

        context.Response.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        context.Response.ContentType.Should().StartWith("application/problem+json");

        var body = await ReadBodyAsync(context);
        body.RootElement.GetProperty("status").GetInt32().Should().Be(502);
        body.RootElement.GetProperty("type").GetString().Should().Be("bad-gateway");
        body.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TimedOutRequestBecomes504ProblemJson()
    {
        var context = await RunAsync(terminal =>
        {
            terminal.Features.Set<IForwarderErrorFeature>(new StubForwarderError(ForwarderError.RequestTimedOut));
            terminal.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            return Task.CompletedTask;
        });

        context.Response.StatusCode.Should().Be(StatusCodes.Status504GatewayTimeout);
        var body = await ReadBodyAsync(context);
        body.RootElement.GetProperty("status").GetInt32().Should().Be(504);
        body.RootElement.GetProperty("type").GetString().Should().Be("gateway-timeout");
    }

    [Fact]
    public async Task DownstreamResponseWithNoForwarderErrorPassesThroughUnmodified()
    {
        const string downstream = """{"type":"validation-failed","title":"Validation failed.","status":400}""";

        var context = await RunAsync(async terminal =>
        {
            // A real downstream response: no IForwarderErrorFeature is set, so the middleware must
            // not re-wrap it — the gateway is a proxy, not the author of these bodies.
            terminal.Response.StatusCode = StatusCodes.Status400BadRequest;
            terminal.Response.ContentType = "application/problem+json";
            await terminal.Response.WriteAsync(downstream);
        });

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var raw = await ReadRawAsync(context);
        raw.Should().Be(downstream);
    }

    private static async Task<HttpContext> RunAsync(RequestDelegate terminal)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var builder = new ApplicationBuilder(services);
        builder.UseForwarderErrorProblemDetails();
        builder.Run(terminal);
        var pipeline = builder.Build();

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        await pipeline(context);
        return context;
    }

    private static async Task<JsonDocument> ReadBodyAsync(HttpContext context) =>
        JsonDocument.Parse(await ReadRawAsync(context));

    private static async Task<string> ReadRawAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private sealed class StubForwarderError(ForwarderError error) : IForwarderErrorFeature
    {
        public ForwarderError Error { get; } = error;

        public Exception? Exception => null;
    }
}
