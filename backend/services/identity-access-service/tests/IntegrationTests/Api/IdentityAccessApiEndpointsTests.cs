using Microsoft.AspNetCore.Mvc;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class IdentityAccessApiEndpointsTests : IAsyncLifetime
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
        bootstrapPayload!.Actor.UserId.Should().BeGreaterThan(0);
        bootstrapPayload.Actor.ExternalIdentityId.Should().Be("kc-user-01");
        bootstrapPayload.Actor.DisplayName.Should().Be("Alice Operator");
        bootstrapPayload.Actor.Email.Should().Be("alice@example.com");
        bootstrapPayload.Actor.Role.Should().Be("Operator");
        bootstrapPayload.Access.Capability.Should().Be("AuthenticatedPlatformAccess");
        bootstrapPayload.Access.IsAllowed.Should().BeTrue();

        var meResponse = await _client.GetAsync("/api/users/me");

        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var mePayload = await meResponse.Content.ReadFromJsonAsync<AuthenticatedActorProfileResponse>();
        mePayload.Should().NotBeNull();
        mePayload!.UserId.Should().BeGreaterThan(0);
        mePayload.ExternalIdentityId.Should().Be("kc-user-01");
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
    public async Task GetUsers_WithAdministratorHeaders_ReturnsPagedCatalog()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);
        await SeedUserAsync("kc-admin-02", "Catalog Administrator", "catalog-admin@example.com", Role.Administrator);
        await SeedUserAsync("kc-participant-01", "Participant User", "participant@example.com", Role.Participant);

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

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
        payload.Items.Select(user => user.DisplayName).Should().ContainInOrder("Admin User", "Catalog Administrator");
        payload.Items.Select(user => user.Role).Should().OnlyContain(role => role == "Administrator");
    }

    [Fact]
    public async Task GetUsers_WithOperatorHeaders_ReturnsOk()
    {
        await SeedUserAsync("kc-operator-01", "Catalog Operator", "operator@example.com", Role.Operator);
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);

        AddTrustedHeaders(
            _client,
            userId: "kc-operator-01",
            role: "Operator",
            email: "operator@example.com");

        var response = await _client.GetAsync("/api/users?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<UserAccessCatalogItemResponse>>();
        payload.Should().NotBeNull();
        payload!.TotalCount.Should().Be(2);
        payload.Items.Should().HaveCount(2);
        payload.Items.Select(user => user.DisplayName).Should().ContainInOrder("Admin User", "Catalog Operator");
    }

    [Theory]
    [InlineData(Role.Administrator)]
    [InlineData(Role.Operator)]
    public async Task RegisterTeam_WithAuthorizedHeaders_ReturnsCreatedAndPersistsTeam(Role actorRole)
    {
        var actorExternalId = actorRole == Role.Administrator ? "kc-admin-01" : "kc-operator-01";
        var actorEmail = actorRole == Role.Administrator ? "admin@example.com" : "operator@example.com";
        await SeedUserAsync(actorExternalId, $"{actorRole} User", actorEmail, actorRole);

        AddTrustedHeaders(
            _client,
            userId: actorExternalId,
            role: actorRole.ToString(),
            email: actorEmail);

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
        var persistedTeam = await dbContext.RegisteredTeams.SingleAsync(storedTeam => storedTeam.TeamId == payload.TeamId);

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
    public async Task RegisterTeam_WithParticipantHeaders_ReturnsForbidden()
    {
        await SeedUserAsync("kc-participant-01", "Participant User", "participant@example.com", Role.Participant);

        AddTrustedHeaders(
            _client,
            userId: "kc-participant-01",
            role: "Participant",
            email: "participant@example.com");

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
    public async Task UpdateTeam_WithOperatorHeaders_ReturnsNoContent()
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

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
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
    public async Task DeactivateTeam_WithOperatorHeaders_ReturnsUpdatedInactiveTeam()
    {
        await SeedUserAsync("kc-operator-01", "Operator User", "operator@example.com", Role.Operator);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");

        AddTrustedHeaders(
            _client,
            userId: "kc-operator-01",
            role: "Operator",
            email: "operator@example.com");

        var response = await _client.DeleteAsync($"/api/teams/{team.TeamId}/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<TeamResponse>();
        payload.Should().NotBeNull();
        payload!.TeamId.Should().Be(team.TeamId);
        payload.IsActive.Should().BeFalse();
    }

    [Theory]
    [InlineData(Role.Administrator)]
    [InlineData(Role.Operator)]
    public async Task AuthorizeParticipantForTeam_WithAuthorizedHeaders_ReturnsCreatedAndPersistsMembership(Role actorRole)
    {
        var actorExternalId = actorRole == Role.Administrator ? "kc-admin-01" : "kc-operator-01";
        var actorEmail = actorRole == Role.Administrator ? "admin@example.com" : "operator@example.com";
        await SeedUserAsync(actorExternalId, $"{actorRole} User", actorEmail, actorRole);
        var participant = await SeedUserAsync("kc-participant-01", "Participant User", "participant@example.com", Role.Participant);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");

        AddTrustedHeaders(
            _client,
            userId: actorExternalId,
            role: actorRole.ToString(),
            email: actorEmail);

        var response = await _client.PostAsJsonAsync(
            $"/api/teams/{team.TeamId}/participants",
            new { userId = participant.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<AuthorizeParticipantForTeamResponse>();
        payload.Should().NotBeNull();
        payload!.TeamMembershipId.Should().NotBe(Guid.Empty);

        var participantsResponse = await _client.GetAsync($"/api/teams/{team.TeamId}/participants");
        participantsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var participants = await participantsResponse.Content.ReadFromJsonAsync<IReadOnlyList<TeamMembershipResponse>>();
        participants.Should().NotBeNull();
        participants!.Should().ContainSingle();
        participants[0].TeamMembershipId.Should().Be(payload.TeamMembershipId);
        participants[0].TeamId.Should().Be(team.TeamId);
        participants[0].UserId.Should().Be(participant.Id);
        participants[0].Email.Should().Be(participant.Email);
        participants[0].DisplayName.Should().Be(participant.DisplayName);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedMembership = await dbContext.RegisteredTeamMemberships.SingleAsync();
        persistedMembership.TeamMembershipId.Should().Be(payload.TeamMembershipId);
        persistedMembership.TeamId.Should().Be(team.TeamId);
        persistedMembership.UserId.Should().Be(participant.Id);
    }

    [Fact]
    public async Task AuthorizeParticipantForTeam_WhenTeamDoesNotExist_ReturnsNotFound()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);
        var participant = await SeedUserAsync("kc-participant-01", "Participant User", "participant@example.com", Role.Participant);

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/teams/{Guid.NewGuid()}/participants",
            new { userId = participant.Id });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthorizeParticipantForTeam_WhenUserDoesNotExist_ReturnsNotFound()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/teams/{team.TeamId}/participants",
            new { userId = 99999 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(Role.Operator)]
    [InlineData(Role.Administrator)]
    public async Task AuthorizeParticipantForTeam_WhenUserIsNotParticipant_ReturnsUnprocessableEntity(Role targetRole)
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);
        var targetUser = await SeedUserAsync($"kc-{targetRole}-01", $"{targetRole} User", $"{targetRole.ToString().ToLowerInvariant()}@example.com", targetRole);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/teams/{team.TeamId}/participants",
            new { userId = targetUser.Id });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problem.Title.Should().Be("Unprocessable entity.");
    }

    [Fact]
    public async Task AuthorizeParticipantForTeam_WhenTeamIsInactive_ReturnsConflict()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);
        var participant = await SeedUserAsync("kc-participant-01", "Participant User", "participant@example.com", Role.Participant);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");
        await DeactivateTeamAsync(team.TeamId);

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/teams/{team.TeamId}/participants",
            new { userId = participant.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AuthorizeParticipantForTeam_WhenDuplicateAssignment_ReturnsConflict()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);
        var participant = await SeedUserAsync("kc-participant-01", "Participant User", "participant@example.com", Role.Participant);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var firstResponse = await _client.PostAsJsonAsync(
            $"/api/teams/{team.TeamId}/participants",
            new { userId = participant.Id });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var secondResponse = await _client.PostAsJsonAsync(
            $"/api/teams/{team.TeamId}/participants",
            new { userId = participant.Id });

        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AuthorizeParticipantForTeam_WithParticipantHeaders_ReturnsForbidden()
    {
        await SeedUserAsync("kc-participant-actor-01", "Participant Actor", "participant.actor@example.com", Role.Participant);
        var participant = await SeedUserAsync("kc-participant-01", "Participant User", "participant@example.com", Role.Participant);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");

        AddTrustedHeaders(
            _client,
            userId: "kc-participant-actor-01",
            role: "Participant",
            email: "participant.actor@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/teams/{team.TeamId}/participants",
            new { userId = participant.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(Role.Administrator)]
    [InlineData(Role.Operator)]
    public async Task GetTeamParticipants_WithAuthorizedHeaders_ReturnsMembershipList(Role actorRole)
    {
        var actorEmail = actorRole == Role.Administrator ? "admin@example.com" : "operator@example.com";
        await SeedUserAsync($"kc-{actorRole}", $"{actorRole} User", actorEmail, actorRole);
        var participantOne = await SeedUserAsync("kc-participant-01", "Participant One", "participant1@example.com", Role.Participant);
        var participantTwo = await SeedUserAsync("kc-participant-02", "Participant Two", "participant2@example.com", Role.Participant);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");

        await AddMembershipAsync(team.TeamId, participantOne.Id);
        await AddMembershipAsync(team.TeamId, participantTwo.Id);

        AddTrustedHeaders(
            _client,
            userId: $"kc-{actorRole}",
            role: actorRole.ToString(),
            email: actorEmail);

        var response = await _client.GetAsync($"/api/teams/{team.TeamId}/participants");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyList<TeamMembershipResponse>>();
        payload.Should().NotBeNull();
        payload!.Should().HaveCount(2);
        payload.Select(item => item.UserId).Should().Contain(new[] { participantOne.Id, participantTwo.Id });
    }

    [Fact]
    public async Task GetTeamParticipants_WithParticipantHeaders_ReturnsForbidden()
    {
        await SeedUserAsync("kc-participant-actor-01", "Participant Actor", "participant.actor@example.com", Role.Participant);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");

        AddTrustedHeaders(
            _client,
            userId: "kc-participant-actor-01",
            role: "Participant",
            email: "participant.actor@example.com");

        var response = await _client.GetAsync($"/api/teams/{team.TeamId}/participants");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTeamParticipants_WhenTeamDoesNotExist_ReturnsNotFound()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var response = await _client.GetAsync($"/api/teams/{Guid.NewGuid()}/participants");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTeamParticipants_WhenTeamHasNoParticipants_ReturnsEmptyList()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var response = await _client.GetAsync($"/api/teams/{team.TeamId}/participants");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyList<TeamMembershipResponse>>();
        payload.Should().NotBeNull();
        payload.Should().BeEmpty();
    }

    [Theory]
    [InlineData(Role.Administrator)]
    [InlineData(Role.Operator)]
    public async Task IssueJoinToken_WithAuthorizedHeaders_ReturnsCreatedAndPersistsHashedToken(Role actorRole)
    {
        var actorEmail = actorRole == Role.Administrator ? "admin@example.com" : "operator@example.com";
        var actorExternalId = actorRole == Role.Administrator ? "kc-admin-01" : "kc-operator-01";
        await SeedUserAsync(actorExternalId, $"{actorRole} User", actorEmail, actorRole);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");
        var liveSessionId = Guid.NewGuid();

        AddTrustedHeaders(
            _client,
            userId: actorExternalId,
            role: actorRole.ToString(),
            email: actorEmail);

        var response = await _client.PostAsJsonAsync(
            "/api/join-tokens",
            new
            {
                liveSessionId,
                teamId = team.TeamId,
                expiresInSeconds = 300
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var payload = await response.Content.ReadFromJsonAsync<IssuedJoinTokenResponse>();
        payload.Should().NotBeNull();
        payload!.JoinTokenId.Should().NotBe(Guid.Empty);
        payload.Token.Should().NotBeNullOrWhiteSpace();
        payload.LiveSessionId.Should().Be(liveSessionId);
        payload.TeamId.Should().Be(team.TeamId);
        payload.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedJoinToken = await dbContext.JoinTokens.SingleAsync(item => item.JoinTokenId == payload.JoinTokenId);

        persistedJoinToken.LiveSessionId.Should().Be(liveSessionId);
        persistedJoinToken.TeamId.Should().Be(team.TeamId);
        persistedJoinToken.TokenHash.Should().NotBe(payload.Token);
        persistedJoinToken.TokenHash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task IssueJoinToken_WithNonAdminOrOperatorHeaders_ReturnsForbidden()
    {
        await SeedUserAsync("kc-participant-01", "Participant User", "participant@example.com", Role.Participant);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");

        AddTrustedHeaders(
            _client,
            userId: "kc-participant-01",
            role: "Participant",
            email: "participant@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/join-tokens",
            new
            {
                liveSessionId = Guid.NewGuid(),
                teamId = team.TeamId,
                expiresInSeconds = 300
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ValidateParticipantMembershipAccess_WithParticipantHeaders_ReturnsAllowedAccessDecision()
    {
        await SeedUserAsync("kc-admin-01", "Admin User", "admin@example.com", Role.Administrator);
        var participant = await SeedUserAsync("kc-participant-01", "Participant User", "participant@example.com", Role.Participant);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");
        await AddMembershipAsync(team.TeamId, participant.Id);
        var liveSessionId = Guid.NewGuid();

        AddTrustedHeaders(
            _client,
            userId: "kc-admin-01",
            role: "Administrator",
            email: "admin@example.com");

        var issuedTokenResponse = await _client.PostAsJsonAsync(
            "/api/join-tokens",
            new
            {
                liveSessionId,
                teamId = team.TeamId,
                expiresInSeconds = 300
            });

        issuedTokenResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var issuedToken = await issuedTokenResponse.Content.ReadFromJsonAsync<IssuedJoinTokenResponse>();
        issuedToken.Should().NotBeNull();

        AddTrustedHeaders(
            _client,
            userId: "kc-participant-01",
            role: "Participant",
            email: "participant@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/permissions/participant-membership-access",
            new
            {
                liveSessionId,
                teamId = team.TeamId,
                token = issuedToken!.Token
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ParticipantMembershipAccessDecisionResponse>();
        payload.Should().NotBeNull();
        payload!.Capability.Should().Be("ParticipantExperience");
        payload.IsAllowed.Should().BeTrue();
        payload.Reason.Should().Contain("validated");
        payload.LiveSessionId.Should().Be(liveSessionId);
        payload.TeamId.Should().Be(team.TeamId);
    }

    [Fact]
    public async Task ValidateParticipantMembershipAccess_WithNonParticipantHeaders_ReturnsForbidden()
    {
        await SeedUserAsync("kc-operator-01", "Operator User", "operator@example.com", Role.Operator);
        var team = await SeedTeamAsync("Red Foxes", "RED-01");

        AddTrustedHeaders(
            _client,
            userId: "kc-operator-01",
            role: "Operator",
            email: "operator@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/permissions/participant-membership-access",
            new
            {
                liveSessionId = Guid.NewGuid(),
                teamId = team.TeamId,
                token = "opaque-token"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ValidateParticipantMembershipAccess_ForForeignTeam_ReturnsForbidden()
    {
        await SeedUserAsync("kc-participant-01", "Participant User", "participant@example.com", Role.Participant);
        var ownTeam = await SeedTeamAsync("Red Foxes", "RED-01");
        var foreignTeam = await SeedTeamAsync("Blue Owls", "BLUE-02");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var participant = await dbContext.Users.SingleAsync(user => user.ExternalIdentityId == "kc-participant-01");
            var team = await dbContext.RegisteredTeams
                .Include(item => item.Memberships)
                .SingleAsync(item => item.TeamId == ownTeam.TeamId);

            team.AuthorizeParticipant(participant.Id);
            await dbContext.SaveChangesAsync();
        }

        AddTrustedHeaders(
            _client,
            userId: "kc-participant-01",
            role: "Participant",
            email: "participant@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/permissions/participant-membership-access",
            new
            {
                liveSessionId = Guid.NewGuid(),
                teamId = foreignTeam.TeamId
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("Forbidden.");
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
        problem.Detail.Should().Contain("deactivated");
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

    private async Task<RegisteredTeam> SeedTeamAsync(string displayName, string teamCode)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var team = RegisteredTeam.Register(displayName, teamCode);

        dbContext.RegisteredTeams.Add(team);
        await dbContext.SaveChangesAsync();
        return team;
    }

    private async Task DeactivateTeamAsync(Guid teamId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var team = await dbContext.RegisteredTeams.SingleAsync(t => t.TeamId == teamId);
        team.Deactivate();
        await dbContext.SaveChangesAsync();
    }

    private async Task AddMembershipAsync(Guid teamId, int userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var team = await dbContext.RegisteredTeams
            .Include(item => item.Memberships)
            .SingleAsync(item => item.TeamId == teamId);

        team.AuthorizeParticipant(userId);
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
        int UserId,
        string ExternalIdentityId,
        string DisplayName,
        string Email,
        string Role,
        bool IsActive);

    private sealed record ProtectedAccessDecisionResponse(
        string Capability,
        bool IsAllowed,
        string Reason);

    private sealed record IssuedJoinTokenResponse(
        Guid JoinTokenId,
        string Token,
        Guid LiveSessionId,
        Guid TeamId,
        DateTimeOffset ExpiresAt);

    private sealed record ParticipantMembershipAccessDecisionResponse(
        string Capability,
        bool IsAllowed,
        string Reason,
        Guid LiveSessionId,
        Guid TeamId);

    private sealed record UserAccessCatalogItemResponse(
        int Id,
        string ExternalIdentityId,
        string DisplayName,
        string Email,
        string Role,
        bool IsActive);

    private sealed record RegisterTeamResponse(Guid TeamId);

    private sealed record AuthorizeParticipantForTeamResponse(Guid TeamMembershipId);

    private sealed record TeamResponse(
        Guid TeamId,
        string DisplayName,
        string TeamCode,
        bool IsActive,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record TeamMembershipResponse(
        Guid TeamMembershipId,
        Guid TeamId,
        int UserId,
        string Email,
        string DisplayName);

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
