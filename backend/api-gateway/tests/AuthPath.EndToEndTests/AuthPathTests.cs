namespace ApiGateway.AuthPath.EndToEndTests;

public sealed class AuthPathTests : IClassFixture<ComposeStackFixture>
{
    private readonly ComposeStackFixture _fixture;

    public AuthPathTests(ComposeStackFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AllowedFrontendOriginPreflightSucceedsBeforeAuthentication()
    {
        await _fixture.ResetProbeAsync();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/test-auth-probe/inspect");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");

        using var response = await _fixture.GatewayClient.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigins).Should().BeTrue();
        allowedOrigins.Should().ContainSingle("http://localhost:3000");
        response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var allowCredentials).Should().BeTrue();
        allowCredentials.Should().ContainSingle("true");
        response.Headers.TryGetValues("Access-Control-Allow-Headers", out var allowedHeaders).Should().BeTrue();
        allowedHeaders.Should().Contain(header =>
            header.Contains("authorization", StringComparison.OrdinalIgnoreCase));

        var state = await _fixture.GetProbeStateAsync();
        state.HitCount.Should().Be(0);
    }

    [Fact]
    public async Task ValidTokenAddsTrustedHeadersAndStripsAuthorization()
    {
        await _fixture.ResetProbeAsync();
        var (accessToken, subject) = await _fixture.GetAdminAccessTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/test-auth-probe/inspect");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _fixture.GatewayClient.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var probeResponse = await response.Content.ReadFromJsonAsync<ProbeInspectResponse>();
        probeResponse.Should().NotBeNull();
        probeResponse!.XUserId.Should().Be(subject);
        probeResponse.XUserRole.Should().Be("Administrator");
        probeResponse.XUserEmail.Should().Be("admin@umbral.local");
        // Keycloak's built-in `profile` scope fills `name` from the seeded first/last name.
        probeResponse.XUserName.Should().Be("Admin Umbral");
        probeResponse.Authorization.Should().BeNullOrEmpty();

        var state = await _fixture.GetProbeStateAsync();
        state.HitCount.Should().Be(1);
    }

    [Fact]
    public async Task TamperedTokenReturnsUnauthorizedAndDoesNotReachDownstream()
    {
        await _fixture.ResetProbeAsync();
        var (accessToken, _) = await _fixture.GetAdminAccessTokenAsync();
        var tamperedToken = accessToken[..^1] + (accessToken[^1] == 'a' ? "b" : "a");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/test-auth-probe/inspect");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);

        using var response = await _fixture.GatewayClient.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var state = await _fixture.GetProbeStateAsync();
        state.HitCount.Should().Be(0);
    }

    private sealed record ProbeInspectResponse(
        [property: JsonPropertyName("xUserId")] string? XUserId,
        [property: JsonPropertyName("xUserRole")] string? XUserRole,
        [property: JsonPropertyName("xUserEmail")] string? XUserEmail,
        [property: JsonPropertyName("xUserName")] string? XUserName,
        [property: JsonPropertyName("authorization")] string? Authorization);
}
