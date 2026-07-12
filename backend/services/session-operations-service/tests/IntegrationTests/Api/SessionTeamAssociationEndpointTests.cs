using Microsoft.AspNetCore.Mvc;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class SessionTeamAssociationEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public SessionTeamAssociationEndpointTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new SessionOperationsApiWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AssociateTeam_WithOperatorRole_PersistsAssociationAndReturnsPayload()
    {
        var liveSession = await SeedScheduledSessionAsync();
        var referenceTeamId = Guid.NewGuid();
        _factory.TeamCatalogClient.Seed(new TeamReferenceDto(referenceTeamId, "Aurora", "AUR-01", true, 3));
        AddTrustedHeaders("operator-123", "Operator", "operator@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId}/teams",
            new { referenceTeamId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<AssociateTeamToSessionResultDto>();
        payload.Should().NotBeNull();
        payload!.LiveSessionId.Should().Be(liveSession.LiveSessionId);
        payload.RuntimeTeamId.Should().NotBe(referenceTeamId);
        payload.ReferenceTeamId.Should().Be(referenceTeamId);
        payload.DisplayName.Should().Be("Aurora");
        payload.TeamCode.Should().Be("AUR-01");
        payload.SessionState.Should().Be(nameof(SessionState.Scheduled));
        payload.AssociatedTeamCount.Should().Be(1);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedSession = await dbContext.LiveSessions
            .Include(session => session.Teams)
            .SingleAsync(session => session.LiveSessionId == liveSession.LiveSessionId);

        persistedSession.Teams.Should().ContainSingle(team =>
            team.TeamId == payload.RuntimeTeamId &&
            team.ReferenceTeamId == referenceTeamId &&
            team.Capacity == 3);
    }

    [Fact]
    public async Task AssociateTeam_WithoutTrustedHeaders_ReturnsUnauthorized()
    {
        var liveSession = await SeedScheduledSessionAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId}/teams",
            new { referenceTeamId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AssociateTeam_WithParticipantRole_ReturnsForbidden()
    {
        var liveSession = await SeedScheduledSessionAsync();
        AddTrustedHeaders("participant-123", "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId}/teams",
            new { referenceTeamId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssociateTeam_WithDuplicateReferenceTeam_ReturnsConflict()
    {
        var liveSession = await SeedScheduledSessionAsync();
        var referenceTeamId = Guid.NewGuid();
        _factory.TeamCatalogClient.Seed(new TeamReferenceDto(referenceTeamId, "Aurora", "AUR-01", true, 2));
        AddTrustedHeaders("operator-123", "Operator", "operator@example.com");

        var firstResponse = await _client.PostAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId}/teams",
            new { referenceTeamId });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var duplicateResponse = await _client.PostAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId}/teams",
            new { referenceTeamId });

        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await duplicateResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Conflict.");
    }

    [Fact]
    public async Task AssociateTeam_WithInactiveTeamReference_ReturnsBadRequest()
    {
        var liveSession = await SeedScheduledSessionAsync();
        var referenceTeamId = Guid.NewGuid();
        _factory.TeamCatalogClient.Seed(new TeamReferenceDto(referenceTeamId, "Dormant", "DRM-01", false, 1));
        AddTrustedHeaders("operator-123", "Operator", "operator@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId}/teams",
            new { referenceTeamId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Validation failed.");
    }

    [Fact]
    public async Task AssociateTeam_WithUnknownTeamReference_ReturnsNotFound()
    {
        var liveSession = await SeedScheduledSessionAsync();
        AddTrustedHeaders("operator-123", "Operator", "operator@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId}/teams",
            new { referenceTeamId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAssociatedTeams_WithOperatorRole_ReturnsPersistedAssociatedTeams()
    {
        var liveSession = await SeedScheduledSessionAsync();
        var firstReferenceTeamId = Guid.NewGuid();
        var secondReferenceTeamId = Guid.NewGuid();

        _factory.TeamCatalogClient.Seed(new TeamReferenceDto(firstReferenceTeamId, "Aurora", "AUR-01", true, 3));
        _factory.TeamCatalogClient.Seed(new TeamReferenceDto(secondReferenceTeamId, "Boreal", "BOR-02", true, 2));
        AddTrustedHeaders("operator-123", "Operator", "operator@example.com");

        await _client.PostAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId}/teams",
            new { referenceTeamId = firstReferenceTeamId });
        await _client.PostAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId}/teams",
            new { referenceTeamId = secondReferenceTeamId });

        var response = await _client.GetAsync($"/api/sessions/{liveSession.LiveSessionId}/teams");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<SessionAssociatedTeamsDto>();
        payload.Should().NotBeNull();
        payload!.LiveSessionId.Should().Be(liveSession.LiveSessionId);
        payload.Teams.Should().HaveCount(2);
        payload.Teams.Should().Contain(team => team.ReferenceTeamId == firstReferenceTeamId && team.DisplayName == "Aurora");
        payload.Teams.Should().Contain(team => team.ReferenceTeamId == secondReferenceTeamId && team.DisplayName == "Boreal");
    }

    [Fact]
    public async Task GetAssociatedTeams_WithParticipantRole_ReturnsForbidden()
    {
        var liveSession = await SeedScheduledSessionAsync();
        AddTrustedHeaders("participant-123", "Participant", "participant@example.com");

        var response = await _client.GetAsync($"/api/sessions/{liveSession.LiveSessionId}/teams");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssociateTeamByCode_WithOperatorRole_PersistsAssociationAndReturnsPayload()
    {
        var liveSession = await SeedScheduledSessionAsync();
        var referenceTeamId = Guid.NewGuid();
        _factory.TeamCatalogClient.Seed(new TeamReferenceDto(referenceTeamId, "Aurora", "AUR-01", true, 3));
        AddTrustedHeaders("operator-123", "Operator", "operator@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/by-code/{liveSession.SessionCode}/teams",
            new { referenceTeamId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<AssociateTeamToSessionResultDto>();
        payload.Should().NotBeNull();
        payload!.LiveSessionId.Should().Be(liveSession.LiveSessionId);
        payload.ReferenceTeamId.Should().Be(referenceTeamId);
        payload.DisplayName.Should().Be("Aurora");
        payload.TeamCode.Should().Be("AUR-01");
        payload.AssociatedTeamCount.Should().Be(1);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedSession = await dbContext.LiveSessions
            .Include(session => session.Teams)
            .SingleAsync(session => session.LiveSessionId == liveSession.LiveSessionId);

        persistedSession.Teams.Should().ContainSingle(team =>
            team.TeamId == payload.RuntimeTeamId &&
            team.ReferenceTeamId == referenceTeamId &&
            team.Capacity == 3);
    }

    [Fact]
    public async Task AssociateTeamByCode_WithUnknownSessionCode_ReturnsNotFound()
    {
        var referenceTeamId = Guid.NewGuid();
        _factory.TeamCatalogClient.Seed(new TeamReferenceDto(referenceTeamId, "Aurora", "AUR-01", true, 3));
        AddTrustedHeaders("operator-123", "Operator", "operator@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/sessions/by-code/NOSUCHCODE/teams",
            new { referenceTeamId });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AssociateTeamByCode_WithParticipantRole_ReturnsForbidden()
    {
        var liveSession = await SeedScheduledSessionAsync();
        AddTrustedHeaders("participant-123", "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/by-code/{liveSession.SessionCode}/teams",
            new { referenceTeamId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAssociatedTeamsByCode_WithOperatorRole_ReturnsPersistedAssociatedTeams()
    {
        var liveSession = await SeedScheduledSessionAsync();
        var firstReferenceTeamId = Guid.NewGuid();
        var secondReferenceTeamId = Guid.NewGuid();

        _factory.TeamCatalogClient.Seed(new TeamReferenceDto(firstReferenceTeamId, "Aurora", "AUR-01", true, 3));
        _factory.TeamCatalogClient.Seed(new TeamReferenceDto(secondReferenceTeamId, "Boreal", "BOR-02", true, 2));
        AddTrustedHeaders("operator-123", "Operator", "operator@example.com");

        await _client.PostAsJsonAsync(
            $"/api/sessions/by-code/{liveSession.SessionCode}/teams",
            new { referenceTeamId = firstReferenceTeamId });
        await _client.PostAsJsonAsync(
            $"/api/sessions/by-code/{liveSession.SessionCode}/teams",
            new { referenceTeamId = secondReferenceTeamId });

        var response = await _client.GetAsync($"/api/sessions/by-code/{liveSession.SessionCode}/teams");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<SessionAssociatedTeamsDto>();
        payload.Should().NotBeNull();
        payload!.LiveSessionId.Should().Be(liveSession.LiveSessionId);
        payload.Teams.Should().HaveCount(2);
        payload.Teams.Should().Contain(team => team.ReferenceTeamId == firstReferenceTeamId && team.DisplayName == "Aurora");
        payload.Teams.Should().Contain(team => team.ReferenceTeamId == secondReferenceTeamId && team.DisplayName == "Boreal");
    }

    private void AddTrustedHeaders(string userId, string role, string email)
    {
        _client.DefaultRequestHeaders.Remove("X-User-Id");
        _client.DefaultRequestHeaders.Remove("X-User-Role");
        _client.DefaultRequestHeaders.Remove("X-User-Email");
        _client.DefaultRequestHeaders.Add("X-User-Id", userId);
        _client.DefaultRequestHeaders.Add("X-User-Role", role);
        _client.DefaultRequestHeaders.Add("X-User-Email", email);
    }

    private async Task<LiveSession> SeedScheduledSessionAsync()
    {
        var sourceMissionId = Guid.NewGuid();
        var liveSession = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Association Session",
            45,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            CreateTreasureHuntSnapshot(sourceMissionId));

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return liveSession;
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(Guid sourceMissionId)
    {
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Seeded Mission",
            MaximumTime.Create(45),
            [
                StageSnapshot.Create("Stage One", 1, [treasureHuntSubstage])
            ],
            [
                TargetSnapshot.Create(
                    treasureHuntSubstage.SubstageSnapshotId,
                    "Target Alpha",
                    "QR-ALPHA",
                    1,
                    true,
                    100,
                    4.711,
                    -74.0721,
                    "Look under the stairs",
                    "AfterPreviousTarget")
            ],
            []);
    }
}
