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
    public async Task GetUsers_WithOperatorHeaders_ReturnsPagedCatalog()
    {
        await SeedUserAsync("kc-operator-01", "Catalog Operator", "operator@example.com", Role.Operator);
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);
        await SeedUserAsync("kc-participant-01", "Participant User", "participant@example.com", Role.Participant);

        AddTrustedHeaders(
            _client,
            userId: "kc-operator-01",
            role: "Operator",
            email: "operator@example.com");

        var response = await _client.GetAsync("/api/users?page=1&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<UserAccessCatalogItemResponse>>();
        payload.Should().NotBeNull();
        payload!.TotalCount.Should().Be(3);
        payload.Page.Should().Be(1);
        payload.PageSize.Should().Be(2);
        payload.TotalPages.Should().Be(2);
        payload.HasPreviousPage.Should().BeFalse();
        payload.HasNextPage.Should().BeTrue();
        payload.Items.Should().HaveCount(2);
        payload.Items.Select(user => user.DisplayName).Should().ContainInOrder("Admin User", "Catalog Operator");
        payload.Items.Select(user => user.Role).Should().Contain(new[] { "Administrator", "Operator" });
    }

    [Fact]
    public async Task RegisterTeam_WithAdministratorHeaders_ReturnsCreatedAndPersistsTeam()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/teams",
            new { displayName = "Red Foxes", teamCode = "RED-01" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var payload = await response.Content.ReadFromJsonAsync<RegisterTeamResponse>();
        payload.Should().NotBeNull();
        payload!.TeamId.Should().NotBe(Guid.Empty);

        var getResponse = await _client.GetAsync($"/api/teams/{payload.TeamId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var team = await getResponse.Content.ReadFromJsonAsync<TeamResponse>();
        team.Should().NotBeNull();
        team!.TeamId.Should().Be(payload.TeamId);
        team.DisplayName.Should().Be("Red Foxes");
        team.TeamCode.Should().Be("RED-01");
        team.IsActive.Should().BeTrue();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedTeam = await dbContext.Teams.SingleAsync(storedTeam => storedTeam.TeamId == payload.TeamId);

        persistedTeam.DisplayName.Should().Be("Red Foxes");
        persistedTeam.TeamCode.Should().Be("RED-01");
        persistedTeam.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterTeam_WithDuplicateTeamCode_ReturnsConflict()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);
        await SeedTeamAsync("Blue Owls", "DUP-01");

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/teams",
            new { displayName = "Red Foxes", teamCode = "DUP-01" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Conflict.");
        problem.Detail.Should().Contain("already exists");
    }

    [Fact]
    public async Task RegisterTeam_WithBlankFields_ReturnsBadRequest()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/teams",
            new { displayName = " ", teamCode = " " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Validation failed.");
    }

    [Fact]
    public async Task RegisterTeam_WithNonAdministratorHeaders_ReturnsForbidden()
    {
        await SeedUserAsync("kc-operator-01", "Operator User", "operator@example.com", Role.Operator);

        AddTrustedHeaders(
            _client,
            userId: "kc-operator-01",
            role: "Operator",
            email: "operator@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/teams",
            new { displayName = "Red Foxes", teamCode = "RED-01" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(Role.Administrator)]
    [InlineData(Role.Operator)]
    public async Task GetTeams_WithAuthorizedHeaders_ReturnsPagedTeams(Role actorRole)
    {
        var actorEmail = actorRole == Role.Administrator ? "admin@example.com" : "operator@example.com";
        await SeedUserAsync($"kc-{actorRole}", $"{actorRole} User", actorEmail, actorRole);
        await SeedTeamAsync("Bravo Unit", "TEAM-02");
        await SeedTeamAsync("Alpha Squad", "TEAM-01");
        var inactiveTeam = await SeedTeamAsync("Charlie Crew", "TEAM-03");
        await DeactivateTeamAsync(inactiveTeam.TeamId);

        AddTrustedHeaders(
            _client,
            userId: $"kc-{actorRole}",
            role: actorRole.ToString(),
            email: actorEmail);

        var response = await _client.GetAsync("/api/teams?page=1&pageSize=3");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<TeamResponse>>();
        payload.Should().NotBeNull();
        payload!.TotalCount.Should().Be(3);
        payload.Page.Should().Be(1);
        payload.PageSize.Should().Be(3);
        payload.Items.Should().HaveCount(3);
        payload.Items.Select(team => team.DisplayName).Should().ContainInOrder("Alpha Squad", "Bravo Unit", "Charlie Crew");
        payload.Items.Should().ContainSingle(team => team.DisplayName == "Charlie Crew" && !team.IsActive);
    }

    [Fact]
    public async Task GetTeams_WithParticipantHeaders_ReturnsForbidden()
    {
        await SeedUserAsync("kc-participant-01", "Participant User", "participant@example.com", Role.Participant);

        AddTrustedHeaders(
            _client,
            userId: "kc-participant-01",
            role: "Participant",
            email: "participant@example.com");

        var response = await _client.GetAsync("/api/teams?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTeamById_WithOperatorHeaders_ReturnsTeam()
    {
        await SeedUserAsync("kc-operator-01", "Operator User", "operator@example.com", Role.Operator);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");

        AddTrustedHeaders(
            _client,
            userId: "kc-operator-01",
            role: "Operator",
            email: "operator@example.com");

        var response = await _client.GetAsync($"/api/teams/{team.TeamId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<TeamResponse>();
        payload.Should().NotBeNull();
        payload!.TeamId.Should().Be(team.TeamId);
        payload.DisplayName.Should().Be("Red Foxes");
        payload.TeamCode.Should().Be("RED-01");
    }

    [Fact]
    public async Task GetTeamById_WhenTeamDoesNotExist_ReturnsNotFound()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var response = await _client.GetAsync($"/api/teams/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateTeam_WithAdministratorHeaders_ReturnsNoContentAndPersistsChanges()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/teams/{team.TeamId}",
            new { displayName = "Blue Owls", teamCode = "BLUE-02" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/teams/{team.TeamId}");
        var payload = await getResponse.Content.ReadFromJsonAsync<TeamResponse>();
        payload.Should().NotBeNull();
        payload!.DisplayName.Should().Be("Blue Owls");
        payload.TeamCode.Should().Be("BLUE-02");
    }

    [Fact]
    public async Task UpdateTeam_WithDuplicateTeamCode_ReturnsConflict()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");
        await SeedTeamAsync("Blue Owls", "BLUE-02");

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/teams/{team.TeamId}",
            new { displayName = "Red Foxes", teamCode = "BLUE-02" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateTeam_WhenTeamDoesNotExist_ReturnsNotFound()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/teams/{Guid.NewGuid()}",
            new { displayName = "Blue Owls", teamCode = "BLUE-02" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateTeam_WithNonAdministratorHeaders_ReturnsForbidden()
    {
        await SeedUserAsync("kc-operator-01", "Operator User", "operator@example.com", Role.Operator);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");

        AddTrustedHeaders(
            _client,
            userId: "kc-operator-01",
            role: "Operator",
            email: "operator@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/teams/{team.TeamId}",
            new { displayName = "Blue Owls", teamCode = "BLUE-02" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeactivateTeam_WithAdministratorHeaders_ReturnsUpdatedInactiveTeam()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var response = await _client.DeleteAsync($"/api/teams/{team.TeamId}/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<TeamResponse>();
        payload.Should().NotBeNull();
        payload!.TeamId.Should().Be(team.TeamId);
        payload.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateTeam_WhenAlreadyInactive_ReturnsConflict()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");
        await DeactivateTeamAsync(team.TeamId);

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var response = await _client.DeleteAsync($"/api/teams/{team.TeamId}/status");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeactivateTeam_WithNonAdministratorHeaders_ReturnsForbidden()
    {
        await SeedUserAsync("kc-operator-01", "Operator User", "operator@example.com", Role.Operator);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");

        AddTrustedHeaders(
            _client,
            userId: "kc-operator-01",
            role: "Operator",
            email: "operator@example.com");

        var response = await _client.DeleteAsync($"/api/teams/{team.TeamId}/status");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignUserRole_WithAdministratorHeaders_ReturnsNoContentAndPersistsRoleChange()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);
        var targetUser = await SeedUserAsync("kc-target-01", "Target User", "target@example.com", Role.Operator);

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/users/{targetUser.Id}/role",
            new { role = "Participant" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persistedUser = await dbContext.Users.SingleAsync(u => u.Id == targetUser.Id);
            persistedUser.Role.Should().Be(Role.Participant);
        }

        var catalogResponse = await _client.GetAsync("/api/users?page=1&pageSize=20");
        catalogResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var catalog = await catalogResponse.Content.ReadFromJsonAsync<PagedResponse<UserAccessCatalogItemResponse>>();
        catalog.Should().NotBeNull();
        catalog!.Items.Should().ContainSingle(user => user.Id == targetUser.Id && user.Role == "Participant");

        AddTrustedHeaders(
            _client,
            userId: "kc-target-01",
            role: "Operator",
            email: "target@example.com");

        var permissionsResponse = await _client.GetAsync("/api/permissions/authenticated-platform-access");

        permissionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var permissionsPayload = await permissionsResponse.Content.ReadFromJsonAsync<ProtectedAccessDecisionResponse>();
        permissionsPayload.Should().NotBeNull();
        permissionsPayload!.Capability.Should().Be("AuthenticatedPlatformAccess");
        permissionsPayload.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task AssignUserRole_WithNonAdministratorHeaders_ReturnsForbidden()
    {
        await SeedUserAsync("kc-operator-01", "Operator User", "operator@example.com", Role.Operator);
        var targetUser = await SeedUserAsync("kc-target-01", "Target User", "target@example.com", Role.Participant);

        AddTrustedHeaders(
            _client,
            userId: "kc-operator-01",
            role: "Operator",
            email: "operator@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/users/{targetUser.Id}/role",
            new { role = "Administrator" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("Forbidden.");
    }

    [Fact]
    public async Task AssignUserRole_WithUnknownRoleValue_ReturnsBadRequest()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);
        var targetUser = await SeedUserAsync("kc-target-01", "Target User", "target@example.com", Role.Operator);

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/users/{targetUser.Id}/role",
            new { role = "SuperAdmin" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Validation failed.");
        problem.Detail.Should().Contain("Role must be a known Role value.");
    }

    [Fact]
    public async Task AssignUserRole_ForDeactivatedTarget_ReturnsUnprocessableEntity()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);
        var targetUser = await SeedUserAsync("kc-target-01", "Target User", "target@example.com", Role.Operator);
        await DeactivateUserAsync(targetUser.Id);

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/users/{targetUser.Id}/role",
            new { role = "Participant" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problem.Title.Should().Be("Unprocessable entity.");
        problem.Detail.Should().Contain("Target user must be active.");
    }

    [Fact]
    public async Task DeactivateUserAccess_WithAdministratorHeaders_ReturnsNoContentAndPersistsInactiveState()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);
        var targetUser = await SeedUserAsync("kc-target-01", "Target User", "target@example.com", Role.Operator);

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var response = await _client.DeleteAsync($"/api/users/{targetUser.Id}/access");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedUser = await dbContext.Users.SingleAsync(u => u.Id == targetUser.Id);

        persistedUser.IsActive.Should().BeFalse();
        persistedUser.ExternalIdentityId.Should().Be("kc-target-01");
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
    public async Task Bootstrap_WithDeactivatedUser_ReturnsForbidden()
    {
        await SeedDeactivatedUserAsync();
        AddTrustedHeaders(
            _client,
            userId: "kc-deactivated-01",
            role: "Operator",
            email: "deactivated@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/users/authenticated",
            new { displayName = "Deactivated Operator" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("Forbidden.");
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
        var user = await SeedUserAsync(
            "kc-deactivated-01",
            "Deactivated Operator",
            "deactivated@example.com",
            Role.Operator);
        await DeactivateUserAsync(user.Id);
    }

    private async Task<User> SeedUserAsync(string externalIdentityId, string displayName, string email, Role role)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = User.Provision(
            externalIdentityId: externalIdentityId,
            displayName: displayName,
            email: email,
            role: role);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    private async Task DeactivateUserAsync(int userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await dbContext.Users.SingleAsync(u => u.Id == userId);
        user.DeactivateAccess();
        await dbContext.SaveChangesAsync();
    }

    private async Task<Team> SeedTeamAsync(string displayName, string teamCode)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var team = Team.Register(displayName, teamCode);

        dbContext.Teams.Add(team);
        await dbContext.SaveChangesAsync();
        return team;
    }

    private async Task DeactivateTeamAsync(Guid teamId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var team = await dbContext.Teams.SingleAsync(t => t.TeamId == teamId);
        team.Deactivate();
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

    private sealed record UserAccessCatalogItemResponse(
        int Id,
        string ExternalIdentityId,
        string DisplayName,
        string Email,
        string Role,
        bool IsActive);

    private sealed record RegisterTeamResponse(Guid TeamId);

    private sealed record TeamResponse(
        Guid TeamId,
        string DisplayName,
        string TeamCode,
        bool IsActive,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record PagedResponse<T>(
        IReadOnlyCollection<T> Items,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages,
        bool HasPreviousPage,
        bool HasNextPage);

    private sealed record HealthStatusResponse(string Status);
}
