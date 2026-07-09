using System.Diagnostics.CodeAnalysis;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the OpenTelemetry listener that makes <c>Activity.Current</c> non-null, so the
/// <c>traceId</c> already read by <see cref="Api.Services.ProblemDetailsExceptionHandler"/> and
/// <see cref="Api.Hubs.DomainExceptionHubFilter"/> becomes a real W3C trace id instead of their
/// per-process fallbacks. Neither of those types changes: they were written to upgrade for free.
/// </summary>
/// <remarks>
/// Traces and logs only — metrics are a non-goal, and Seq rejects the OTLP metrics path outright.
/// Logs travel over OTLP through <c>builder.Logging.AddOpenTelemetry</c>, so every existing
/// <c>ILogger&lt;T&gt;</c> call site keeps working and no Serilog enters the dependency graph.
/// </remarks>
[ExcludeFromCodeCoverage]
public static class ObservabilityExtensions
{
    private const string OtlpEndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>
    /// Wires tracing and log export, or does nothing at all when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>
    /// is unset.
    /// </summary>
    /// <remarks>
    /// The guard is not an optimisation. <c>SessionOperationsApiWebApplicationFactory</c> boots the
    /// real <c>AddWebServices</c>, so without it every container-backed integration test would pay
    /// OTLP exporter startup and retry against an endpoint that does not exist. Registering nothing
    /// keeps that boot byte-identical to a pre-OpenTelemetry one.
    /// </remarks>
    public static void AddObservability(this IHostApplicationBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointVariable]))
        {
            return;
        }

        // Endpoint, protocol and service name all come from the OTEL_* environment, so no service
        // here needs an appsettings.json it does not already have. Seq speaks OTLP over HTTP only:
        // the exporter's default gRPC transport fails against it with HTTP_1_1_REQUIRED, and fails
        // silently, so OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf is required alongside the endpoint.
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetSampler(new AlwaysOnSampler())
                // AspNetCore instrumentation also registers the SignalR server ActivitySource, so a
                // hub invocation gets its own trace id without naming that source here (spike S1).
                .AddAspNetCoreInstrumentation()
                // The eight typed HttpClients already inject traceparent via DiagnosticsHandler once
                // a listener exists; this records the outbound span for each hop.
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddOtlpExporter());

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.AddOtlpExporter();
        });
    }
}
