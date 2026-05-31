using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Infrastructure.Identity;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        builder.AddPersistenceServices();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IIdentityService, IdentityService>();
    }
}
