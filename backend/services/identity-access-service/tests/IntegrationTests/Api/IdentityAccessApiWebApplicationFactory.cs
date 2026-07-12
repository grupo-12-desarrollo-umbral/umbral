using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Enums;
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

        // No Keycloak in this test host: swap the real identity-provider sync (which would time out
        // and trip the fail-loud path -> 503) for a no-op that reports success. These tests verify the
        // HTTP/persistence contract, not Keycloak behaviour.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IIdentityProviderAdminService>();
            services.AddSingleton<IIdentityProviderAdminService, NoOpIdentityProviderAdminService>();
        });
    }

    private sealed class NoOpIdentityProviderAdminService : IIdentityProviderAdminService
    {
        public Task<string> CreateUserAsync(string email, CancellationToken cancellationToken)
            => Task.FromResult($"kc-{Guid.NewGuid():N}");

        public Task SendExecuteActionsEmailAsync(string externalIdentityId, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task DeleteUserAsync(string externalIdentityId, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SyncUserRoleAsync(string externalIdentityId, Role newRole, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SyncUserActiveStateAsync(string externalIdentityId, bool isActive, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE users, registered_teams RESTART IDENTITY CASCADE;");
    }

    protected override void Dispose(bool disposing)
    {
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, null);
        base.Dispose(disposing);
    }
}
