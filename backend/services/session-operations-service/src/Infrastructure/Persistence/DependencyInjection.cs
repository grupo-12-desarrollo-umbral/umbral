using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Interceptors;
using umbral_backend.Infrastructure.Persistence.Repositories;

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

        builder.Services.AddScoped<AuditableEntityInterceptor>();
        builder.Services.AddScoped<DispatchDomainEventsInterceptor>();

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            // Add ONLY the app's own interceptors, by concrete type. Resolving the whole
            // ISaveChangesInterceptor collection here would also pull in MassTransit's bus-outbox
            // interceptor, whose construction resolves ApplicationDbContext and re-enters this factory —
            // an infinite loop / StackOverflow. MassTransit attaches its outbox interceptor to the
            // DbContext itself via AddEntityFrameworkOutbox, so it must not be added again here.
            options.AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<DispatchDomainEventsInterceptor>());
            options.UseNpgsql(connectionString);
        });

        builder.Services.AddScoped<IDatabaseHealthCheck, DatabaseHealthCheck>();
        builder.Services.AddScoped<ILiveSessionRepository, LiveSessionRepository>();
    }
}
