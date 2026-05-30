namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddGatewayServices(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = builder.Configuration["Keycloak:Authority"];
                options.Audience = builder.Configuration["Keycloak:Audience"];
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                options.BackchannelTimeout = TimeSpan.FromSeconds(5);
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

        builder.Services
            .AddReverseProxy()
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
            .AddTransforms(transformBuilderContext =>
                transformBuilderContext.RequestTransforms.Add(new TrustedHeadersTransform()));
    }
}
