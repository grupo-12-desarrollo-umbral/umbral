using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the OpenTelemetry listener that gives the gateway a root span for the first hop, so a
/// trace no longer begins at whichever service YARP forwarded to. The trace id itself already
/// reached the services without this — YARP's <c>SocketsHttpHandler</c> injects <c>traceparent</c>
/// regardless (spike S2) — what the listener adds is the gateway's own latency and the span that
/// everything downstream hangs off.
/// </summary>
/// <remarks>
/// Traces and logs only — metrics are a non-goal, and Seq rejects the OTLP metrics path outright.
/// Logs travel over OTLP through <c>builder.Logging.AddOpenTelemetry</c>, so every existing
/// <c>ILogger&lt;T&gt;</c> call site keeps working and no Serilog enters the dependency graph.
/// No <c>AddNpgsql</c> here, unlike the three services: the gateway owns no database.
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
    /// The gateway has no <c>WebApplicationFactory</c>, so the guard does not protect an in-process
    /// test boot the way it does in the three services. It is kept because <c>AuthPath.EndToEndTests</c>
    /// boots this container for real, and because one unset-guard contract across all four components
    /// is worth more than the registrations it would save.
    /// </remarks>
    public static void AddObservability(this IHostApplicationBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointVariable]))
        {
            return;
        }

        // Endpoint, protocol and service name all come from the OTEL_* environment, so neither of the
        // gateway's appsettings files gains an OTEL section. Seq speaks OTLP over HTTP only: the
        // exporter's default gRPC transport fails against it with HTTP_1_1_REQUIRED, and fails
        // silently, so OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf is required alongside the endpoint.
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                // AlwaysOn per the Phase 0 sampling decision for dev. Now that the gateway records,
                // it forwards traceparent with the sampled flag on, which is the precondition a
                // future switch to ParentBased needs (spike S2).
                .SetSampler(new AlwaysOnSampler())
                .AddAspNetCoreInstrumentation()
                // Covers both outbound paths: YARP's forwarder, whose SocketsHttpHandler carries the
                // runtime DiagnosticsHandler that HttpClient instrumentation listens to, and the
                // JwtBearer backchannel's Keycloak metadata fetch.
                .AddHttpClientInstrumentation()
                .AddOtlpExporter());

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.AddOtlpExporter();
        });
    }
}
