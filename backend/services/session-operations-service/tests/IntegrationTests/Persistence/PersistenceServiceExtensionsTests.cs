using Microsoft.Extensions.Hosting;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

public sealed class PersistenceServiceExtensionsTests
{
    [Fact]
    public void AddPersistenceServices_WhenConnectionStringIsMissing_ThrowsInvalidOperationException()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = [],
            EnvironmentName = Environments.Development,
        });

        var act = () => builder.AddPersistenceServices();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*umbral_backendDb*not found*");
    }
}
