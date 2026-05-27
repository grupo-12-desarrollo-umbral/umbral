using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using umbral_backend.Infrastructure.Identity.Keycloak;

namespace Microsoft.Extensions.DependencyInjection;

public static class IdentityServiceExtensions
{
    public static void AddIdentityServices(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<KeycloakOptions>(
            builder.Configuration.GetSection(KeycloakOptions.SectionName));

        var keycloakOptions = builder.Configuration.GetSection(KeycloakOptions.SectionName).Get<KeycloakOptions>()
            ?? new KeycloakOptions();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = keycloakOptions.Authority;
                options.MetadataAddress = keycloakOptions.MetadataAddress;
                options.RequireHttpsMetadata = keycloakOptions.RequireHttpsMetadata;
                options.TokenValidationParameters.ValidAudience = keycloakOptions.ClientId;
                options.Events = new KeycloakJwtEvents();
            });

        builder.Services.AddAuthorization();
    }
}
