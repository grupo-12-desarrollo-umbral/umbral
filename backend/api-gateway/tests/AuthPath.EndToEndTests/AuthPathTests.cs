namespace ApiGateway.AuthPath.EndToEndTests;

public sealed class AuthPathTests : IClassFixture<ComposeStackFixture>
{
    private readonly ComposeStackFixture _fixture;

    public AuthPathTests(ComposeStackFixture fixture)
    {
        _fixture = fixture;
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
        probeResponse.XUserRole.Should().Be("Administrador");
        probeResponse.XUserEmail.Should().Be("admin@umbral.local");
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
        [property: JsonPropertyName("authorization")] string? Authorization);
}
