using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public const string FrontendCorsPolicyName = "FrontendSignalR";

    // Applied to the anonymous /api/users/register route (ADR-0016 §1): hosted registration would have
    // supplied bot/rate protection for free, so we add it here for the custom endpoint. Partitioned by
    // client IP so one address cannot spray account creation or probe which emails already exist.
    public const string RegisterRateLimiterPolicyName = "register";

    public static void AddGatewayServices(this IHostApplicationBuilder builder)
    {
        // Strip access_token (and friends) from the request-logging and forwarder records that would
        // otherwise write a replayable JWT to stdout — SignalR sends its token in the query (ADR-0002).
        // Redaction happens inside the ILoggerFactory decorator, so it covers the OTLP exporter that
        // AddObservability registers below just as it covers the console sink, whatever the order here.
        RedactingLoggerFactory.AddQueryParameterRedaction(builder.Logging);

        builder.AddObservability();

        var frontendOrigins = builder.Configuration
            .GetSection("Frontend:AllowedOrigins")
            .Get<string[]>()?
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(FrontendCorsPolicyName, policy =>
            {
                if (frontendOrigins.Length == 0)
                {
                    return;
                }

                policy
                    .WithOrigins(frontendOrigins)
                    .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Options)
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = builder.Configuration["Keycloak:Authority"];
                options.Audience = builder.Configuration["Keycloak:Audience"];
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                options.BackchannelTimeout = TimeSpan.FromSeconds(5);
                options.BackchannelHttpHandler = new RewriteLocalhostBackchannelHandler(new SocketsHttpHandler());
                options.MetadataAddress = $"{builder.Configuration["Keycloak:Authority"]}/.well-known/openid-configuration";
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuers = new[]
                    {
                        "http://localhost:8080/realms/umbral",
                        "http://keycloak:8080/realms/umbral"
                    },
                    ValidateAudience = true,
                    ValidAudience = builder.Configuration["Keycloak:Audience"]
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = WebSocketTokenExtractionTransform.OnMessageReceived,
                    OnAuthenticationFailed = context =>
                    {
                        // Only intercept infrastructure failures; token validation failures
                        // (bad signature, expired, malformed payload, etc.) fall through so the
                        // framework produces the standard 401 challenge.
                        var isInfrastructureFailure =
                            context.Exception is HttpRequestException ||
                            (context.Exception is InvalidOperationException &&
                             context.Exception.Message.Contains("IDX20803"));

                        if (!isInfrastructureFailure)
                        {
                            return Task.CompletedTask;
                        }

                        context.Fail(context.Exception);
                        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                        context.Response.ContentType = "application/problem+json";
                        return context.Response.WriteAsync("""
                            {
                              "type": "https://httpstatuses.org/503",
                              "title": "Upstream Unavailable",
                              "detail": "The authentication provider is temporarily unreachable. Retry later.",
                              "status": 503
                            }
                            """);
                    }
                };
            });

        builder.Services.AddAuthorization();

        builder.Services.AddRateLimiter(options =>
        {
            // 429 (not the default 503) so a throttled registrant gets the semantically correct status;
            // problem+json keeps the body consistent with every other gateway error response.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(RegisterRateLimiterPolicyName, httpContext =>
            {
                var clientKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(clientKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(15),
                    QueueLimit = 0,
                });
            });
        });

        // Mirrors the three domain services: an unhandled exception becomes RFC 7807 problem+json
        // instead of a bare framework 500. UseExceptionHandler in Program.cs activates it.
        builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

        builder.Services
            .AddReverseProxy()
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
            .AddTransforms(transformBuilderContext =>
                transformBuilderContext.RequestTransforms.Add(new TrustedHeadersTransform()));
    }
}
