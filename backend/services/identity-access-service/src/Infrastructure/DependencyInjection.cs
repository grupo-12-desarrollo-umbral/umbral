using Microsoft.Extensions.DependencyInjection.Extensions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Infrastructure.Identity;
using umbral_backend.Infrastructure.Identity.Keycloak;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        builder.AddPersistenceServices();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.TryAddScoped<ICurrentUser, CurrentUser>();

        builder.Services.Configure<KeycloakOptions>(
            builder.Configuration.GetSection(KeycloakOptions.SectionName));

        builder.Services.AddHttpClient<IKeycloakAdminService, KeycloakAdminService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
    }
}

