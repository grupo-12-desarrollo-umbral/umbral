using Microsoft.AspNetCore.Hosting;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

public sealed class MissionDesignApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string ConnectionStringEnvironmentVariable = "ConnectionStrings__umbral_backendDb";
    private readonly string _connectionString;

    public MissionDesignApiWebApplicationFactory(string connectionString)
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
            "TRUNCATE TABLE \"TriviaOptions\", \"TriviaQuestions\", \"TriviaQuizzes\", \"Missions\" RESTART IDENTITY CASCADE;");
    }

    protected override void Dispose(bool disposing)
    {
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, null);
        base.Dispose(disposing);
    }
}
