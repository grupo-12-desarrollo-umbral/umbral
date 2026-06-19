using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using umbral_backend.Infrastructure.Integrations.MissionDesign;
using umbral_backend.Infrastructure.IntegrationTests.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Integrations.MissionDesign;

public sealed class MissionReadinessSourceIntegrationTests
{
    [Fact]
    public async Task GetByIdAsync_WhenMissionIsReady_ReturnsActiveAndReadyFacts()
    {
        using var host = await BuildHostAsync(app =>
        {
            app.MapGet("/api/missions/{id:int}/readiness", (int id) =>
                Results.Ok(new
                {
                    MissionId = id,
                    ActivationState = "Ready",
                    IsReady = true,
                    Failures = Array.Empty<string>()
                }));
        });

        var source = new MissionReadinessSource(host.GetTestClient(), TestCurrentUser.Default);

        var readiness = await source.GetByIdAsync(7, CancellationToken.None);

        readiness.Should().NotBeNull();
        readiness!.MissionId.Should().Be(7);
        readiness.ActivationState.Should().Be("Ready");
        readiness.IsActive.Should().BeTrue();
        readiness.IsReady.Should().BeTrue();
        readiness.Failures.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_WhenMissionIsInactive_ReportsNotActive()
    {
        using var host = await BuildHostAsync(app =>
        {
            app.MapGet("/api/missions/{id:int}/readiness", (int id) =>
                Results.Ok(new
                {
                    MissionId = id,
                    ActivationState = "Inactive",
                    IsReady = false,
                    Failures = new[] { "Mission is inactive" }
                }));
        });

        var source = new MissionReadinessSource(host.GetTestClient(), TestCurrentUser.Default);

        var readiness = await source.GetByIdAsync(9, CancellationToken.None);

        readiness.Should().NotBeNull();
        readiness!.ActivationState.Should().Be("Inactive");
        readiness.IsActive.Should().BeFalse();
        readiness.IsReady.Should().BeFalse();
        readiness.Failures.Should().ContainSingle();
    }

    [Fact]
    public async Task GetByIdAsync_WhenMissionDoesNotExist_ReturnsNull()
    {
        using var host = await BuildHostAsync(app =>
        {
            app.MapGet("/api/missions/{id:int}/readiness", () => Results.StatusCode((int)HttpStatusCode.NotFound));
        });

        var source = new MissionReadinessSource(host.GetTestClient(), TestCurrentUser.Default);

        var readiness = await source.GetByIdAsync(404, CancellationToken.None);

        readiness.Should().BeNull();
    }

    private static async Task<IHost> BuildHostAsync(Action<WebApplication> configureRoutes)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
        });

        var app = builder.Build();
        configureRoutes(app);
        await app.StartAsync();
        return app;
    }
}
