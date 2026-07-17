using umbral_backend.Application.Permissions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

// Covers the controller-level ASP.NET policies (F8): they are the second enforcement layer over the
// MediatR AuthorizeAttribute, so these assert the policies reject at the edge AND that the two
// deliberate carve-outs survive — the anonymous routes, and the reason-coded permissions routes that
// must answer 200-with-a-reason rather than 403.
[Collection(PostgreSqlCollection.Name)]
public sealed class AuthorizationPolicyEnforcementTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private IdentityAccessApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public AuthorizationPolicyEnforcementTests(PostgreSqlFixture fixture)
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

    [Theory]
    [InlineData("/api/teams")]
    [InlineData("/api/users")]
    [InlineData("/api/users/me")]
    [InlineData("/api/permissions/authenticated-platform-access")]
    public async Task ProtectedRoutes_WithoutTrustedHeaders_ReturnUnauthorized(string route)
    {
        var response = await _client.GetAsync(route);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Teams_WithParticipantHeaders_ReturnsForbidden()
    {
        AddTrustedHeaders(_client, "kc-participant-01", "Participant", "participant@example.com");

        var response = await _client.GetAsync("/api/teams");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUsers_WithParticipantHeaders_ReturnsForbidden()
    {
        AddTrustedHeaders(_client, "kc-participant-01", "Participant", "participant@example.com");

        var response = await _client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // The admin-only routes: an Operator passes the service's baseline authenticated check but must
    // still be rejected, which is what separates the Administrator policy from AdminOrOperator.
    [Fact]
    public async Task InviteUser_WithOperatorHeaders_ReturnsForbidden()
    {
        AddTrustedHeaders(_client, "kc-operator-01", "Operator", "operator@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/users/invitations",
            new { email = "invitee@example.com", role = "Operator" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // No target seeded on purpose: the policy must reject before the handler looks anything up, so a
    // non-existent id still yields 403 rather than 404.
    [Fact]
    public async Task DeactivateUserAccess_WithOperatorHeaders_ReturnsForbidden()
    {
        AddTrustedHeaders(_client, "kc-operator-01", "Operator", "operator@example.com");

        var response = await _client.DeleteAsync("/api/users/4242/access");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // A policy rejection short-circuits before MediatR, so without the ProblemDetails wiring it would
    // return an empty body. The error shape must not depend on which layer rejected the caller.
    [Fact]
    public async Task PolicyRejection_ReturnsProblemDetailsMatchingTheMediatRLayer()
    {
        AddTrustedHeaders(_client, "kc-participant-01", "Participant", "participant@example.com");

        var response = await _client.GetAsync("/api/teams");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("Forbidden.");
    }

    // ADR-0016 §1: these two are anonymous by design — the caller has no account yet. A controller
    // baseline [Authorize] would silently break self-registration, so the [AllowAnonymous] opt-outs
    // are load-bearing.
    [Fact]
    public async Task RegisterParticipant_WithoutTrustedHeaders_StaysAnonymous()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/users/register",
            new { displayName = "Nina Participant", email = "nina@example.com", password = "Sup3r-Secret!" });

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ForgotPassword_WithoutTrustedHeaders_StaysAnonymous()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/users/forgot-password",
            new { email = "nobody@example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // The contract these routes exist for: a non-participant is a reason-coded 200, never a 403. A
    // role policy here would collapse `UserNotParticipant` into an error the callers cannot read.
    [Fact]
    public async Task ParticipantEligibleTeams_WithOperatorHeaders_ReturnsReasonCodedDenialNotForbidden()
    {
        await SeedUserAsync("kc-operator-01", "operator@example.com", Role.Operator);

        AddTrustedHeaders(_client, "kc-operator-01", "Operator", "operator@example.com");

        var response = await _client.GetAsync("/api/permissions/participant-eligible-teams");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<EligibleTeamsResponse>();
        payload.Should().NotBeNull();
        payload!.IsEligible.Should().BeFalse();
        payload.ReasonCode.Should().Be(ParticipantMembershipAccessReasonCodes.UserNotParticipant);
    }

    private async Task<User> SeedUserAsync(string externalIdentityId, string email, Role role)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = User.Invite(externalIdentityId, email, role);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return user;
    }

    private static void AddTrustedHeaders(HttpClient client, string userId, string role, string email)
    {
        client.DefaultRequestHeaders.Remove("X-User-Id");
        client.DefaultRequestHeaders.Remove("X-User-Role");
        client.DefaultRequestHeaders.Remove("X-User-Email");

        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-User-Role", role);
        client.DefaultRequestHeaders.Add("X-User-Email", email);
    }

    private sealed record EligibleTeamsResponse(bool IsEligible, string ReasonCode);
}
