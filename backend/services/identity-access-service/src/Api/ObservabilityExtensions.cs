using System.Diagnostics.CodeAnalysis;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the OpenTelemetry listener that makes <c>Activity.Current</c> non-null, so the
/// <c>traceId</c> already read by <see cref="Api.Services.ProblemDetailsExceptionHandler"/>
/// becomes a real W3C trace id instead of its per-process fallback. That type does not change:
/// it was written to upgrade for free.
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
    /// The guard is not an optimisation. <c>IdentityAccessApiWebApplicationFactory</c> boots the
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

        // Endpoint, protocol and service name all come from the OTEL_* environment, which is why this
        // service needs no appsettings.json it does not already have — it has none. Seq speaks OTLP
        // over HTTP only: the exporter's default gRPC transport fails against it with
        // HTTP_1_1_REQUIRED, and fails silently, so OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf is
        // required alongside the endpoint.
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                // AlwaysOn is the pinned dev sampling decision (Phase 0). A future switch to
                // ParentBased must instrument a trace's root before its children, or the root
                // forwards traceparent as -00 and ParentBased drops every downstream span.
                .SetSampler(new AlwaysOnSampler())
                .AddAspNetCoreInstrumentation()
                // The typed KeycloakAdminService client already injects traceparent via
                // DiagnosticsHandler once a listener exists; this records the outbound span.
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
