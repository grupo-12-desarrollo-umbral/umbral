using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Web.Endpoints;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

public sealed class MissionEndpointsTests : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private MissionDesignApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public MissionEndpointsTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new MissionDesignApiWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task CreateMission_ReturnsCreatedMissionWithDraftSourceReadiness()
    {
        AddAdministratorHeaders();

        var response = await _client.PostAsJsonAsync(
            "/api/missions/",
            new
            {
                name = "Mission Atlas",
                description = "Locate the relay point.",
                difficulty = "Advanced",
                maximumTimeMinutes = 50
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var payload = await response.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        payload.Should().NotBeNull();
        payload!.Name.Should().Be("Mission Atlas");
        payload.Description.Should().Be("Locate the relay point.");
        payload.Difficulty.Should().Be("Advanced");
        payload.MaximumTimeMinutes.Should().Be(50);
        payload.IsActive.Should().BeTrue();
        payload.ActivationState.Should().Be("Draft");
        payload.IsSourceReady.Should().BeFalse();
    }

    [Fact]
    public async Task GetMissionCatalogAndDetail_ReturnInactiveMissionAsUnavailableForNewSessions()
    {
        AddAdministratorHeaders();

        var missionId = await CreateMissionAsync("Mission Atlas");

        var deactivateResponse = await _client.DeleteAsync($"/api/missions/{missionId}");
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var catalogResponse = await _client.GetAsync("/api/missions/");
        catalogResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var catalog = await catalogResponse.Content.ReadFromJsonAsync<IReadOnlyList<MissionsEndpoints.MissionSummaryResponse>>();
        catalog.Should().NotBeNull();
        catalog!.Should().ContainSingle();
        catalog[0].Id.Should().Be(missionId);
        catalog[0].ActivationState.Should().Be("Inactive");
        catalog[0].IsActive.Should().BeFalse();
        catalog[0].IsSourceReady.Should().BeFalse();

        var detailResponse = await _client.GetAsync($"/api/missions/{missionId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await detailResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(missionId);
        detail.ActivationState.Should().Be("Inactive");
        detail.IsActive.Should().BeFalse();
        detail.IsSourceReady.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateMission_ReturnsUpdatedMission()
    {
        AddAdministratorHeaders();

        var missionId = await CreateMissionAsync("Mission Before");

        var response = await _client.PutAsJsonAsync(
            $"/api/missions/{missionId}",
            new
            {
                name = "Mission After",
                description = "Updated briefing.",
                difficulty = "Beginner",
                maximumTimeMinutes = 25
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        payload.Should().NotBeNull();
        payload!.Id.Should().Be(missionId);
        payload.Name.Should().Be("Mission After");
        payload.Description.Should().Be("Updated briefing.");
        payload.Difficulty.Should().Be("Beginner");
        payload.MaximumTimeMinutes.Should().Be(25);
        payload.ActivationState.Should().Be("Draft");
        payload.IsSourceReady.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateMission_WithInvalidPayload_ReturnsBadRequest()
    {
        AddAdministratorHeaders();

        var missionId = await CreateMissionAsync("Mission Before");

        var response = await _client.PutAsJsonAsync(
            $"/api/missions/{missionId}",
            new
            {
                name = "",
                description = "",
                difficulty = "",
                maximumTimeMinutes = 0
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Validation failed.");
    }

    [Fact]
    public async Task UpdateMission_WhenMissionDoesNotExist_ReturnsNotFound()
    {
        AddAdministratorHeaders();

        var response = await _client.PutAsJsonAsync(
            "/api/missions/999",
            new
            {
                name = "Missing Mission",
                description = "Missing briefing.",
                difficulty = "Advanced",
                maximumTimeMinutes = 45
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Title.Should().Be("Resource not found.");
    }

    [Fact]
    public async Task DeactivateMission_WhenMissionDoesNotExist_ReturnsNotFound()
    {
        AddAdministratorHeaders();

        var response = await _client.DeleteAsync("/api/missions/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Title.Should().Be("Resource not found.");
    }

    [Fact]
    public async Task DeactivateMission_WithInvalidRouteValue_ReturnsBadRequest()
    {
        AddAdministratorHeaders();

        var response = await _client.DeleteAsync("/api/missions/0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Validation failed.");
    }

    private void AddAdministratorHeaders()
    {
        _client.DefaultRequestHeaders.Remove("X-User-Id");
        _client.DefaultRequestHeaders.Remove("X-User-Role");
        _client.DefaultRequestHeaders.Add("X-User-Id", "admin-01");
        _client.DefaultRequestHeaders.Add("X-User-Role", "Administrator");
    }

    private async Task<int> CreateMissionAsync(string name)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/missions/",
            new
            {
                name,
                description = "Mission briefing.",
                difficulty = "Advanced",
                maximumTimeMinutes = 45
            });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        payload.Should().NotBeNull();

        return payload!.Id;
    }
}
