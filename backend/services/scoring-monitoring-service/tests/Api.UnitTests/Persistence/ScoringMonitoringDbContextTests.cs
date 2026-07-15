using Microsoft.EntityFrameworkCore;
using umbral_backend.Domain.Entities;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.ScoringMonitoring.Api.UnitTests.Persistence;

public sealed class ScoringMonitoringDbContextTests
{
    [Fact]
    public void Penalties_DbSet_IsAccessible()
    {
        var options = new DbContextOptionsBuilder<ScoringMonitoringDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .Options;

        using var context = new ScoringMonitoringDbContext(options);

        context.Penalties.Should().NotBeNull();
    }
}
