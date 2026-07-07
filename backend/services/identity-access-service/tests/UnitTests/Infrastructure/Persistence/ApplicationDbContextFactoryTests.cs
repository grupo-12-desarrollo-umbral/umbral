using Microsoft.EntityFrameworkCore;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Application.UnitTests.Infrastructure.Persistence;

public sealed class ApplicationDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_UsesEnvironmentVariableWhenPresent()
    {
        const string connectionString = "Host=postgres;Database=identity_access_service;Username=postgres;Password=postgres";
        Environment.SetEnvironmentVariable("IDENTITY_ACCESS_SERVICE_CONNECTION_STRING", connectionString);

        try
        {
            var factory = new ApplicationDbContextFactory();

            using var context = factory.CreateDbContext([]);

            context.Database.GetConnectionString().Should().Be(connectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable("IDENTITY_ACCESS_SERVICE_CONNECTION_STRING", null);
        }
    }

    [Fact]
    public void CreateDbContext_FallsBackToDesignTimeConnectionString_WhenEnvironmentVariableAbsent()
    {
        Environment.SetEnvironmentVariable("IDENTITY_ACCESS_SERVICE_CONNECTION_STRING", null);

        var factory = new ApplicationDbContextFactory();

        using var context = factory.CreateDbContext([]);

        context.Database.GetConnectionString().Should().Contain("Database=identity_access_service");
    }
}
