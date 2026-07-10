using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

public sealed class ApplicationDbContextFactoryTests
{
    private const string EnvironmentVariableName = "SESSION_OPERATIONS_SERVICE_CONNECTION_STRING";

    [Fact]
    public void CreateDbContext_WhenEnvironmentConnectionStringIsSet_UsesEnvironmentValue()
    {
        const string connectionString = "Host=localhost;Database=session_operations_env;Username=postgres;Password=postgres";
        using var _ = new EnvironmentVariableScope(EnvironmentVariableName, connectionString);
        var factory = new ApplicationDbContextFactory();

        using var context = factory.CreateDbContext(Array.Empty<string>());

        context.Database.GetConnectionString().Should().Be(connectionString);
    }

    [Fact]
    public void CreateDbContext_WhenEnvironmentConnectionStringIsUnset_UsesLocalDefault()
    {
        using var _ = new EnvironmentVariableScope(EnvironmentVariableName, null);
        var factory = new ApplicationDbContextFactory();

        using var context = factory.CreateDbContext(Array.Empty<string>());

        context.Database.GetConnectionString().Should().Be(
            "Host=localhost;Database=session_operations_service;Username=postgres;Password=postgres");
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previousValue;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _previousValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _previousValue);
        }
    }
}
