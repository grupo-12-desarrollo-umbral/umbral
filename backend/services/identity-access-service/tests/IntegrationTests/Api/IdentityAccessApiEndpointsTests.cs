using Microsoft.AspNetCore.Mvc;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

public sealed class IdentityAccessApiEndpointsTests : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private IdentityAccessApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public IdentityAccessApiEndpointsTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new IdentityAccessApiWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task BootstrapAndReadCurrentUser_ReturnsProvisionedProfileAndRole()
    {
        AddTrustedHeaders(
            _client,
            userId: "kc-user-01",
            role: "Operator",
            email: "alice@example.com");

        var bootstrapResponse = await _client.PostAsJsonAsync(
            "/api/users/authenticated",
            new { displayName = "Alice Operator" });

        bootstrapResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var bootstrapPayload = await bootstrapResponse.Content.ReadFromJsonAsync<BootstrapResponse>();
        bootstrapPayload.Should().NotBeNull();
        bootstrapPayload!.Actor.ExternalIdentityId.Should().Be("kc-user-01");
        bootstrapPayload.Actor.DisplayName.Should().Be("Alice Operator");
        bootstrapPayload.Actor.Email.Should().Be("alice@example.com");
        bootstrapPayload.Actor.Role.Should().Be("Operator");
        bootstrapPayload.Access.Capability.Should().Be("AuthenticatedPlatformAccess");
        bootstrapPayload.Access.IsAllowed.Should().BeTrue();

        var meResponse = await _client.GetAsync("/api/users/me");

        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var mePayload = await meResponse.Content.ReadFromJsonAsync<AuthenticatedActorProfileResponse>();
        mePayload.Should().NotBeNull();
        mePayload!.ExternalIdentityId.Should().Be("kc-user-01");
        mePayload.Role.Should().Be("Operator");
        mePayload.IsActive.Should().BeTrue();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedUser = await dbContext.Users.SingleAsync();

        persistedUser.ExternalIdentityId.Should().Be("kc-user-01");
        persistedUser.DisplayName.Should().Be("Alice Operator");
        persistedUser.Email.Should().Be("alice@example.com");
        persistedUser.Role.Should().Be(Role.Operator);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutTrustedHeaders_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/permissions/authenticated-platform-access");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problem.Title.Should().Be("Unauthorized.");
    }

    [Fact]
    public async Task ProtectedEndpoint_WithDeactivatedUser_ReturnsForbidden()
    {
        await SeedDeactivatedUserAsync();
        AddTrustedHeaders(
            _client,
            userId: "kc-deactivated-01",
            role: "Operator",
            email: "deactivated@example.com");

        var response = await _client.GetAsync("/api/permissions/authenticated-platform-access");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("Forbidden.");
    }

    [Fact]
    public async Task Bootstrap_WithoutTrustedHeaders_ReturnsUnauthorized()
    {
        ClearTrustedHeaders(_client);

        var response = await _client.PostAsJsonAsync(
            "/api/users/authenticated",
            new { displayName = "No Headers" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problem.Title.Should().Be("Unauthorized.");
    }

    [Fact]
    public async Task HealthEndpoint_WhenDatabaseAvailable_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<HealthStatusResponse>();
        payload.Should().NotBeNull();
        payload!.Status.Should().Be("Healthy");
    }

    [Fact]
    public async Task AliveEndpoint_ReturnsAlive()
    {
        var response = await _client.GetAsync("/alive");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<HealthStatusResponse>();
        payload.Should().NotBeNull();
        payload!.Status.Should().Be("Alive");
    }

    private async Task SeedDeactivatedUserAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = User.Provision(
            externalIdentityId: "kc-deactivated-01",
            displayName: "Deactivated Operator",
            email: "deactivated@example.com",
            role: Role.Operator);

        user.DeactivateAccess();

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
    }

    private static void AddTrustedHeaders(HttpClient client, string userId, string role, string email)
    {
        ClearTrustedHeaders(client);

        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-User-Role", role);
        client.DefaultRequestHeaders.Add("X-User-Email", email);
    }

    private static void ClearTrustedHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-User-Id");
        client.DefaultRequestHeaders.Remove("X-User-Role");
        client.DefaultRequestHeaders.Remove("X-User-Email");
    }

    private sealed record BootstrapResponse(
        AuthenticatedActorProfileResponse Actor,
        ProtectedAccessDecisionResponse Access);

    private sealed record AuthenticatedActorProfileResponse(
        string ExternalIdentityId,
        string DisplayName,
        string Email,
        string Role,
        bool IsActive);

    private sealed record ProtectedAccessDecisionResponse(
        string Capability,
        bool IsAllowed,
        string Reason);

    private sealed record HealthStatusResponse(string Status);
}
