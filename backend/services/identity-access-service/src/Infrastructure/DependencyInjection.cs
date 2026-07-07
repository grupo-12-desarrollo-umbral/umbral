using Microsoft.Extensions.DependencyInjection.Extensions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Infrastructure.Identity;
using umbral_backend.Infrastructure.Identity.Keycloak;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        builder.AddPersistenceServices();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.TryAddScoped<ICurrentUser, CurrentUser>();

        builder.Services.AddOptions<KeycloakOptions>()
            .Bind(builder.Configuration.GetSection(KeycloakOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<KeycloakOptions>>(
            new KeycloakOptionsValidator(builder.Environment.IsDevelopment()));

        builder.Services.AddHttpClient<IIdentityProviderAdminService, KeycloakAdminService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
    }
}

