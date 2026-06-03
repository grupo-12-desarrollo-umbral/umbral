using Microsoft.AspNetCore.Hosting;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

public sealed class IdentityAccessApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string ConnectionStringEnvironmentVariable = "ConnectionStrings__umbral_backendDb";
    private readonly string _connectionString;

    public IdentityAccessApiWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, _connectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE identity_provider_sessions, join_tokens, session_team_associations, live_sessions, users, teams RESTART IDENTITY CASCADE;");
    }

    protected override void Dispose(bool disposing)
    {
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, null);
        base.Dispose(disposing);
    }
}
