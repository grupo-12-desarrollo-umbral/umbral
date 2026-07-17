namespace ApiGateway.IntegrationTests;

// Boots the gateway in-process (TestServer) and probes /health. The point of these tests is the two
// properties an orchestrator's liveness check depends on: the endpoint answers 200, and it does so
// with no bearer token — i.e. the auth middleware never challenges the probe.
public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        // OTLP unset keeps AddObservability's exporters out of the in-process boot; /health is anonymous
        // so JwtBearer never reaches for Keycloak metadata. Development is the WAF default environment.
        _factory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", null));
    }

    [Fact]
    public async Task HealthReturnsOk()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthIsReachableWithoutAuthentication()
    {
        using var client = _factory.CreateClient();

        // No Authorization header: an unauthenticated probe must not get a 401 from the auth middleware.
        using var response = await client.GetAsync("/health");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HealthReportsHealthyStatus()
    {
        using var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<HealthResponse>("/health");

        body.Should().NotBeNull();
        body!.Status.Should().Be("healthy");
    }

    private sealed record HealthResponse(string Status);
}
