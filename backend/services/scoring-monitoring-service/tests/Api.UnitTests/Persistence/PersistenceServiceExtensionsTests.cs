using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace umbral_backend.ScoringMonitoring.Api.UnitTests.Persistence;

public sealed class PersistenceServiceExtensionsTests
{
    [Fact]
    public void AddPersistenceServices_WhenConnectionStringIsNull_ThrowsInvalidOperationException()
    {
        var configuration = new Mock<IConfigurationManager>();
        configuration
            .Setup(c => c.GetSection(It.IsAny<string>()))
            .Returns(new Mock<IConfigurationSection>().Object);

        var builder = new Mock<IHostApplicationBuilder>();
        builder.Setup(b => b.Configuration).Returns(configuration.Object);
        builder.Setup(b => b.Services).Returns(new ServiceCollection());

        var act = () => builder.Object.AddPersistenceServices();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Connection string 'umbral_backendDb' not found.");
    }

    [Fact]
    public void AddPersistenceServices_WhenConnectionStringIsEmpty_ThrowsInvalidOperationException()
    {
        var configuration = new Mock<IConfigurationManager>();
        configuration
            .Setup(c => c["ConnectionStrings:umbral_backendDb"])
            .Returns(string.Empty);
        configuration
            .Setup(c => c.GetSection(It.IsAny<string>()))
            .Returns(new Mock<IConfigurationSection>().Object);

        var builder = new Mock<IHostApplicationBuilder>();
        builder.Setup(b => b.Configuration).Returns(configuration.Object);
        builder.Setup(b => b.Services).Returns(new ServiceCollection());

        var act = () => builder.Object.AddPersistenceServices();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddPersistenceServices_WhenConnectionStringIsWhiteSpace_ThrowsInvalidOperationException()
    {
        var configuration = new Mock<IConfigurationManager>();
        configuration
            .Setup(c => c["ConnectionStrings:umbral_backendDb"])
            .Returns("   ");
        configuration
            .Setup(c => c.GetSection(It.IsAny<string>()))
            .Returns(new Mock<IConfigurationSection>().Object);

        var builder = new Mock<IHostApplicationBuilder>();
        builder.Setup(b => b.Configuration).Returns(configuration.Object);
        builder.Setup(b => b.Services).Returns(new ServiceCollection());

        var act = () => builder.Object.AddPersistenceServices();

        act.Should().Throw<InvalidOperationException>();
    }
}
