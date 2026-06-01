using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Interceptors;
using umbral_backend.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class PersistenceServiceExtensions
{
    public static void AddPersistenceServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("umbral_backendDb");
        Guard.Against.Null(connectionString, message: $"Connection string 'umbral_backendDb' not found.");

        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString);
        });

        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        builder.Services.AddScoped<IMissionRepository, MissionRepository>();
        builder.Services.AddScoped<IMissionReadModelRepository, MissionReadModelRepository>();
        builder.Services.AddScoped<ITriviaQuizRepository, TriviaQuizRepository>();
        builder.Services.AddScoped<ITriviaQuizReadModelRepository, TriviaQuizReadModelRepository>();

        builder.Services.AddScoped<ApplicationDbContextInitialiser>();
    }
}
