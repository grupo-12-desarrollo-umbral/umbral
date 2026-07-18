using Microsoft.EntityFrameworkCore.Design;

namespace umbral_backend.Infrastructure.Persistence;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    // Design-time only (dotnet ef migrations/tooling). At runtime the app resolves its real
    // connection string via AddPersistenceServices; this fallback must never reach production.
    private const string DesignTimeFallbackConnectionString =
        "Host=localhost;Database=users_service;Username=postgres;Password=postgres";

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("USERS_SERVICE_CONNECTION_STRING")
            ?? DesignTimeFallbackConnectionString;

        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
