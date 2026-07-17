using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using umbral_backend.Api.Services;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

// Covers F6: mission-design authorizes through the MediatR AuthorizationBehaviour, and until now had
// no ASP.NET auth middleware — so an ASP.NET [Authorize] on a controller here would have silently
// passed every request. No production controller carries a policy yet; the point of the middleware is
// that one *could*. These tests pin that down with a probe controller mounted only in the test host,
// so the guarantee is verified without inventing a production route to hang it on.
[Collection("MissionDesignIntegrationTests")]
public sealed class AuthorizationMiddlewareTests : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private PolicyProbeWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public AuthorizationMiddlewareTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new PolicyProbeWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task PolicyDecoratedEndpoint_WithoutTrustedHeaders_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync(PolicyProbeController.AdministratorOrOperatorRoute);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PolicyDecoratedEndpoint_WithWrongRole_ReturnsForbidden()
    {
        AddTrustedHeaders(role: "Participant");

        var response = await _client.GetAsync(PolicyProbeController.AdministratorOrOperatorRoute);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Operator")]
    public async Task PolicyDecoratedEndpoint_WithAllowedRole_ReturnsOk(string role)
    {
        AddTrustedHeaders(role);

        var response = await _client.GetAsync(PolicyProbeController.AdministratorOrOperatorRoute);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // The gateway forwards X-User-Email only when the token carries an email claim, so requiring it
    // would 401 a Keycloak user without one. mission-design's ICurrentUser never exposes Email.
    [Fact]
    public async Task PolicyDecoratedEndpoint_WithoutEmailHeader_Authenticates()
    {
        AddTrustedHeaders(role: "Operator");

        var response = await _client.GetAsync(PolicyProbeController.AdministratorOrOperatorRoute);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // A middleware rejection short-circuits before MediatR, so without the ProblemDetails wiring it
    // would return an empty body — inconsistent with every other error this service emits.
    [Fact]
    public async Task PolicyRejection_ReturnsProblemDetailsMatchingTheMediatRLayer()
    {
        AddTrustedHeaders(role: "Participant");

        var response = await _client.GetAsync(PolicyProbeController.AdministratorOrOperatorRoute);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("Forbidden.");
    }

    private void AddTrustedHeaders(string role)
    {
        _client.DefaultRequestHeaders.Remove("X-User-Id");
        _client.DefaultRequestHeaders.Remove("X-User-Role");

        _client.DefaultRequestHeaders.Add("X-User-Id", "kc-user-01");
        _client.DefaultRequestHeaders.Add("X-User-Role", role);
    }
}

// Mounts PolicyProbeController into the real Program pipeline, so the probe exercises the same
// authentication scheme and policies the service registers rather than a stand-in.
sealed class PolicyProbeWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string ConnectionStringEnvironmentVariable = "ConnectionStrings__umbral_backendDb";

    public PolicyProbeWebApplicationFactory(string connectionString)
    {
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, connectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
            services.AddControllers()
                .PartManager.ApplicationParts.Add(new AssemblyPart(typeof(PolicyProbeController).Assembly)));
    }

    protected override void Dispose(bool disposing)
    {
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, null);
        base.Dispose(disposing);
    }
}

[ApiController]
[Route("test-only/policy-probe")]
public sealed class PolicyProbeController : ControllerBase
{
    public const string AdministratorOrOperatorRoute = "/test-only/policy-probe/administrator-or-operator";

    [HttpGet("administrator-or-operator")]
    [Authorize(Policy = AuthorizationPolicies.AdministratorOrOperator)]
    public IActionResult AdministratorOrOperator() => Ok();
}
