using Microsoft.AspNetCore.Mvc;
using umbral_backend.Application.Dtos.Sessions;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

// HU-23: the participant team-board GET returns the team-scoped board snapshot, guarded by
// the participant-membership access fact. The board includes current score (or zero), the
// authoritative timer, active-substage target progress, and optional visible clues.
[Collection(PostgreSqlCollection.Name)]
public sealed class ParticipantTeamBoardEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public ParticipantTeamBoardEndpointTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new SessionOperationsApiWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
        _factory.AccessClient.IsAllowed = true;
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetTeamBoard_ForAuthorizedParticipant_ReturnsOkWithBoardSnapshot()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.GetAsync(BuildTeamBoardUrl(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ParticipantTeamBoardDto>();
        payload.Should().NotBeNull();
        payload!.LiveSessionId.Should().Be(seeded.LiveSessionId);
        payload.TeamId.Should().Be(seeded.TeamId);
        payload.TeamDisplayName.Should().Be("Alpha");
        payload.TeamCode.Should().Be("A-01");
        payload.CurrentScore.Should().Be(0);
        payload.Timer.Should().NotBeNull();
        payload.Timer.SessionState.Should().Be(nameof(SessionState.Active));
        payload.ActiveSubstage.Should().NotBeNull();
        payload.ActiveSubstage!.PlayMode.Should().Be(nameof(SubstagePlayMode.TreasureHunt));
        payload.ActiveSubstage.TotalActiveTargets.Should().BeGreaterThan(0);
        payload.ActiveSubstage.ResolvedTargets.Should().Be(0);
        payload.VisibleClues.Should().NotBeNull();
        payload.ActiveTargets.Should().NotBeEmpty();
        payload.ActiveTargets.Should().OnlyContain(target =>
            target.Latitude == 4.711 && target.Longitude == -74.0721);
    }

    [Fact]
    public async Task GetTeamBoard_WhenAccessFactDenied_ReturnsForbiddenProblemDetails()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        _factory.AccessClient.IsAllowed = false;
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.GetAsync(BuildTeamBoardUrl(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task GetTeamBoard_WhenSessionDoesNotExist_ReturnsNotFound()
    {
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.GetAsync(
            $"/api/sessions/{Guid.NewGuid():D}/participants/team-board?teamId={Guid.NewGuid():D}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTeamBoard_WithoutTrustedHeaders_ReturnsUnauthorized()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();

        var response = await _client.GetAsync(BuildTeamBoardUrl(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTeamBoard_WithOperatorRole_ReturnsForbidden()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        AddTrustedHeaders(_client, "kc-operator-1", "Operator", "operator@example.com");

        var response = await _client.GetAsync(BuildTeamBoardUrl(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<SeededSession> SeedActiveTreasureHuntSessionAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTimeOffset.UtcNow;
        var createdAt = now.AddMinutes(-20);
        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"BD-{Guid.NewGuid():N}"[..12],
            "Team Board Test",
            maximumTimeMinutes: 45,
            createdAt,
            CreateTreasureHuntSnapshot(sourceMissionId));
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);

        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(1), policy);
        session.MoveTo(SessionState.Active, now, policy);

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(session.LiveSessionId, team.TeamId);
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(Guid sourceMissionId)
    {
        var substage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);
        var target = TargetSnapshot.Create(
            substage.SubstageSnapshotId,
            "Find the key",
            "KEY-001",
            1,
            isActive: true,
            100,
            4.711,
            -74.0721,
            "Look near the entrance.",
            "VisibleAtStart");

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Board Test Mission",
            MaximumTime.Create(45),
            [
                StageSnapshot.Create("Stage One", 1, [substage])
            ],
            [target],
            []);
    }

    private static string BuildTeamBoardUrl(SeededSession seeded)
    {
        return $"/api/sessions/{seeded.LiveSessionId:D}/participants/team-board?teamId={seeded.TeamId:D}";
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

    private sealed record SeededSession(Guid LiveSessionId, Guid TeamId);
}
