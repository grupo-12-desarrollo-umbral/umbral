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
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = WebSocketTokenExtractionTransform.OnMessageReceived
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
