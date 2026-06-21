using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class AssignOperatorToSessionEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public AssignOperatorToSessionEndpointTests(PostgreSqlFixture fixture)
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
    public async Task AssignOperator_WithAdministratorCaller_ReturnsUpdatedAssignmentState()
    {
        var liveSession = await SeedSessionAsync();
        _factory.AssignableSessionOperatorAccessClient.IsEligible = true;
        AddTrustedHeaders(_client, "99", "Administrator", "admin@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId:D}/operator-assignment",
            new
            {
                operatorUserId = 27
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<AssignOperatorToSessionResponse>();
        payload.Should().NotBeNull();
        payload!.LiveSessionId.Should().Be(liveSession.LiveSessionId);
        payload.AssignedOperatorUserId.Should().Be(27);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedSession = await dbContext.LiveSessions
            .SingleAsync(session => session.LiveSessionId == liveSession.LiveSessionId);

        persistedSession.AssignedOperatorUserId.Should().Be(27);
    }

    [Fact]
    public async Task AssignOperator_WithExistingAssignment_ReassignsOperator()
    {
        var liveSession = await SeedSessionAsync(session => session.AssignOperator(27, DateTimeOffset.UtcNow.AddMinutes(-5)));
        _factory.AssignableSessionOperatorAccessClient.IsEligible = true;
        AddTrustedHeaders(_client, "99", "Administrator", "admin@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId:D}/operator-assignment",
            new
            {
                operatorUserId = 31
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<AssignOperatorToSessionResponse>();
        payload.Should().NotBeNull();
        payload!.AssignedOperatorUserId.Should().Be(31);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedSession = await dbContext.LiveSessions
            .SingleAsync(session => session.LiveSessionId == liveSession.LiveSessionId);

        persistedSession.AssignedOperatorUserId.Should().Be(31);
    }

    [Fact]
    public async Task AssignOperator_WithoutTrustedHeaders_ReturnsUnauthorized()
    {
        var liveSession = await SeedSessionAsync();

        var response = await _client.PatchAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId:D}/operator-assignment",
            new
            {
                operatorUserId = 27
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AssignOperator_WithNonAdministratorRole_ReturnsForbidden()
    {
        var liveSession = await SeedSessionAsync();
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Operator", "operator@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId:D}/operator-assignment",
            new
            {
                operatorUserId = 27
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignOperator_WithIneligibleTargetActor_ReturnsBadRequest()
    {
        var liveSession = await SeedSessionAsync();
        _factory.AssignableSessionOperatorAccessClient.IsEligible = false;
        _factory.AssignableSessionOperatorAccessClient.Role = "Participant";
        _factory.AssignableSessionOperatorAccessClient.Reason = "User is not an operator or administrator.";
        AddTrustedHeaders(_client, "99", "Administrator", "admin@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId:D}/operator-assignment",
            new
            {
                operatorUserId = 27
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Bad request.");
        problem.Detail.Should().Contain("not eligible");
    }

    [Fact]
    public async Task AssignOperator_WithUnknownSession_ReturnsNotFound()
    {
        _factory.AssignableSessionOperatorAccessClient.IsEligible = true;
        AddTrustedHeaders(_client, "99", "Administrator", "admin@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/sessions/{Guid.NewGuid():D}/operator-assignment",
            new
            {
                operatorUserId = 27
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Resource not found.");
    }

    private async Task<LiveSession> SeedSessionAsync(Action<LiveSession>? mutate = null)
    {
        var liveSession = CreateSession();
        mutate?.Invoke(liveSession);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync();

        return liveSession;
    }

    private static LiveSession CreateSession()
    {
        var sourceMissionId = Guid.NewGuid();
        return LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Operator Assignment Session",
            45,
            DateTimeOffset.UtcNow.AddHours(2),
            CreateTreasureHuntSnapshot(sourceMissionId));
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(Guid sourceMissionId)
    {
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1, 100);

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
                    "Look under the stairs",
                    "AfterPreviousTarget")
            ],
            []);
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

    private sealed record AssignOperatorToSessionResponse(
        Guid LiveSessionId,
        int AssignedOperatorUserId);
}
