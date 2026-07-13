using Microsoft.AspNetCore.Mvc;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class ReleaseClueEndpointTests : IAsyncLifetime
{
    private const int OperatorUserId = 55;
    private const string OperatorExternalIdentityId = "kc-operator-55";

    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public ReleaseClueEndpointTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new SessionOperationsApiWebApplicationFactory(_fixture.ConnectionString);
        _factory.AccessClient.IsAllowed = true;
        _factory.AuthenticatedActorProfileAccessClient.CurrentActor = new AuthenticatedActorProfileLookupDto(
            OperatorUserId,
            OperatorExternalIdentityId,
            "Operator",
            true);
        _client = _factory.CreateClient();
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ReleaseClue_ToSingleTeam_ReturnsOkAndReleasedTeamId()
    {
        var seeded = await SeedActiveTreasureHuntSessionWithOperatorAsync();
        AddTrustedHeaders(OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/clues/release",
            new { targetId = seeded.TargetSnapshotId, teamId = seeded.PrimaryTeamId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ReleaseClueResultDto>();
        payload.Should().NotBeNull();
        payload!.TargetId.Should().Be(seeded.TargetSnapshotId);
        payload.ReleasedTeamIds.Should().ContainSingle(id => id == seeded.PrimaryTeamId);
    }

    [Fact]
    public async Task ReleaseClue_ToAllTeams_ReturnsOkAndAllTeamIds()
    {
        var seeded = await SeedActiveTreasureHuntSessionWithOperatorAsync();
        AddTrustedHeaders(OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/clues/release",
            new { targetId = seeded.TargetSnapshotId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ReleaseClueResultDto>();
        payload.Should().NotBeNull();
        payload!.TargetId.Should().Be(seeded.TargetSnapshotId);
        payload.ReleasedTeamIds.Should().HaveCount(2);
        payload.ReleasedTeamIds.Should().Contain(seeded.PrimaryTeamId);
        payload.ReleasedTeamIds.Should().Contain(seeded.SecondaryTeamId);
    }

    [Fact]
    public async Task ReleaseClue_WithoutTrustedHeaders_ReturnsUnauthorized()
    {
        var seeded = await SeedActiveTreasureHuntSessionWithOperatorAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/clues/release",
            new { targetId = seeded.TargetSnapshotId, teamId = seeded.PrimaryTeamId });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReleaseClue_WithParticipantRole_ReturnsForbidden()
    {
        var seeded = await SeedActiveTreasureHuntSessionWithOperatorAsync();
        AddTrustedHeaders("participant-123", "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/clues/release",
            new { targetId = seeded.TargetSnapshotId, teamId = seeded.PrimaryTeamId });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReleaseClue_DuplicateRelease_ReturnsConflict()
    {
        var seeded = await SeedActiveTreasureHuntSessionWithOperatorAsync();
        AddTrustedHeaders(OperatorExternalIdentityId, "Operator", "operator@example.com");

        var firstResponse = await _client.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/clues/release",
            new { targetId = seeded.TargetSnapshotId, teamId = seeded.PrimaryTeamId });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var duplicateResponse = await _client.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/clues/release",
            new { targetId = seeded.TargetSnapshotId, teamId = seeded.PrimaryTeamId });

        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await duplicateResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Conflict.");
    }

    [Fact]
    public async Task ReleaseClue_NotActiveSession_ReturnsConflict()
    {
        var seeded = await SeedScheduledSessionWithOperatorAsync();
        AddTrustedHeaders(OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/clues/release",
            new { targetId = seeded.TargetSnapshotId, teamId = seeded.PrimaryTeamId });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Conflict.");
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

    private async Task<SeededSession> SeedActiveTreasureHuntSessionWithOperatorAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var sourceMissionId = Guid.NewGuid();

        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"CLR-{Guid.NewGuid():N}"[..12],
            "Clue Release Test",
            45,
            createdAt,
            CreateTreasureHuntSnapshot(sourceMissionId));

        var primaryTeam = session.AssociateTeam(Guid.NewGuid(), "Alpha", "ALP-01", 4);
        var secondaryTeam = session.AssociateTeam(Guid.NewGuid(), "Bravo", "BRV-01", 4);
        session.AssignOperator(OperatorUserId, createdAt.AddMinutes(1));

        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(1), transitionPolicy);
        session.MoveTo(SessionState.Active, createdAt.AddMinutes(2), transitionPolicy);

        var targetSnapshotId = session.MissionRuntimeSnapshot.TargetSnapshots.First().TargetSnapshotId;

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return new SeededSession(session.LiveSessionId, targetSnapshotId, primaryTeam.TeamId, secondaryTeam.TeamId);
    }

    private async Task<SeededSession> SeedScheduledSessionWithOperatorAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var sourceMissionId = Guid.NewGuid();

        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SCL-{Guid.NewGuid():N}"[..12],
            "Scheduled Clue Release Test",
            45,
            createdAt,
            CreateTreasureHuntSnapshot(sourceMissionId));

        var primaryTeam = session.AssociateTeam(Guid.NewGuid(), "Sierra", "SRA-01", 4);
        var secondaryTeam = session.AssociateTeam(Guid.NewGuid(), "Tango", "TNG-01", 4);
        session.AssignOperator(OperatorUserId, createdAt.AddMinutes(1));

        var targetSnapshotId = session.MissionRuntimeSnapshot.TargetSnapshots.First().TargetSnapshotId;

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return new SeededSession(session.LiveSessionId, targetSnapshotId, primaryTeam.TeamId, secondaryTeam.TeamId);
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(Guid sourceMissionId)
    {
        var substage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Clue Release Mission",
            MaximumTime.Create(45),
            [
                StageSnapshot.Create("Stage One", 1, [substage])
            ],
            [
                TargetSnapshot.Create(
                    substage.SubstageSnapshotId,
                    "Hidden Clue Target",
                    "QR-HIDDEN",
                    1,
                    isActive: true,
                    100,
                    4.711,
                    -74.0721,
                    "The treasure lies beneath.",
                    "HiddenUntilOperatorRelease")
            ],
            []);
    }

    private sealed record SeededSession(
        Guid LiveSessionId,
        Guid TargetSnapshotId,
        Guid PrimaryTeamId,
        Guid SecondaryTeamId);
}
