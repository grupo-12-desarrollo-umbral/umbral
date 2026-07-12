using System.Diagnostics.CodeAnalysis;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the OpenTelemetry listener that makes <c>Activity.Current</c> non-null, so the
/// <c>traceId</c> already read by <see cref="Api.Services.ProblemDetailsExceptionHandler"/> becomes
/// a real W3C trace id instead of its per-process fallback. That type does not change: it was
/// written to upgrade for free.
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
    /// is unset — keeping the container-backed integration-test boot byte-identical to a
    /// pre-OpenTelemetry one (no exporter startup against a non-existent endpoint).
    /// </summary>
    public static void AddObservability(this IHostApplicationBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointVariable]))
        {
            return;
        }

        // Endpoint, protocol and service name all come from the OTEL_* environment. Seq speaks OTLP
        // over HTTP only, so OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf is required alongside the
        // endpoint (the exporter's default gRPC transport fails silently against it).
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetSampler(new AlwaysOnSampler())
                .AddAspNetCoreInstrumentation()
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
