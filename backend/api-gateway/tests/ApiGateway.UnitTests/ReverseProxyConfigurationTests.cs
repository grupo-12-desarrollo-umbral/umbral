using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace ApiGateway.UnitTests;

public sealed class ReverseProxyConfigurationTests
{
    [Fact]
    public void ProductionRoutesTriviaRequestsToMissionDesign()
    {
        using var production = LoadConfiguration("appsettings.json");

        var triviaRoute = Routes(production).GetProperty("trivia-design");

        triviaRoute.GetProperty("ClusterId").GetString().Should().Be("mission-design");
        triviaRoute.GetProperty("Match").GetProperty("Path").GetString()
            .Should().Be("/api/trivias/{**catch-all}");
        triviaRoute.GetProperty("AuthorizationPolicy").GetString().Should().Be("default");
    }

    [Fact]
    public void ProductionRoutesSessionHistoryRequestsToScoring()
    {
        using var production = LoadConfiguration("appsettings.json");

        var historyRoute = Routes(production).GetProperty("scoring-session-history");

        historyRoute.GetProperty("ClusterId").GetString().Should().Be("scoring");
        historyRoute.GetProperty("Match").GetProperty("Path").GetString()
            .Should().Be("/api/sessions/{liveSessionId}/history");
        historyRoute.GetProperty("AuthorizationPolicy").GetString().Should().Be("default");
    }

    [Fact]
    public void ProductionContainsEveryDevelopmentRoute()
    {
        using var production = LoadConfiguration("appsettings.json");
        using var development = LoadConfiguration("appsettings.Development.json");

        var productionRouteNames = Routes(production).EnumerateObject().Select(route => route.Name);
        var developmentRouteNames = Routes(development).EnumerateObject().Select(route => route.Name);

        developmentRouteNames.Except(productionRouteNames).Should().BeEmpty();
    }

    private static JsonDocument LoadConfiguration(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Configuration", fileName);
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static JsonElement Routes(JsonDocument configuration) =>
        configuration.RootElement.GetProperty("ReverseProxy").GetProperty("Routes");
}
