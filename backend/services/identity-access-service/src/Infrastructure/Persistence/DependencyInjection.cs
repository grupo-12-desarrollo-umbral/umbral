using Microsoft.EntityFrameworkCore.Diagnostics;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Interceptors;
using umbral_backend.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class PersistenceServiceExtensions
{
    public static void AddPersistenceServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("umbral_backendDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'umbral_backendDb' not found.");
        }

        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString);
        });

        builder.Services.AddScoped<IDatabaseHealthCheck, DatabaseHealthCheck>();
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<ITeamRepository, TeamRepository>();
        builder.Services.AddScoped<ILiveSessionReferenceRepository, LiveSessionReferenceRepository>();
        builder.Services.AddScoped<IJoinTokenRepository, JoinTokenRepository>();
    }
}
