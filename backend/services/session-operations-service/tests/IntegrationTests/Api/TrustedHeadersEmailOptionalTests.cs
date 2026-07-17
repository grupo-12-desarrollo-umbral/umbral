using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

// The gateway forwards X-User-Email only when the token carries an email claim
// (api-gateway/src/Transforms/TrustedHeadersTransform.cs), but the trusted-headers handler used to
// require it — so a Keycloak user with no email claim was 401'd out of every session-ops route.
// Nothing here consumes the email, so it is authenticated as optional.
[Collection(PostgreSqlCollection.Name)]
public sealed class TrustedHeadersEmailOptionalTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public TrustedHeadersEmailOptionalTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new SessionOperationsApiWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
        _factory.AuthenticatedActorProfileAccessClient.CurrentActor = new AuthenticatedActorProfileLookupDto(
            27,
            "kc-operator-27",
            "Operator",
            true);
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task PolicyDecoratedEndpoint_WithoutEmailHeader_Authenticates()
    {
        _client.DefaultRequestHeaders.Add("X-User-Id", "kc-operator-27");
        _client.DefaultRequestHeaders.Add("X-User-Role", "Operator");

        var response = await _client.GetAsync("/api/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Id and Role stay mandatory — the policies are built on the role claim, so a missing one must
    // still be a rejection rather than an unauthenticated request slipping through.
    [Fact]
    public async Task PolicyDecoratedEndpoint_WithoutRoleHeader_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Add("X-User-Id", "kc-operator-27");
        _client.DefaultRequestHeaders.Add("X-User-Email", "operator@example.com");

        var response = await _client.GetAsync("/api/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
