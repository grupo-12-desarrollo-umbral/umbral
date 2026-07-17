using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

/// <summary>
/// Pins the PATCH /api/sessions/{id}/state endpoint: a valid transition returns 200 with the previous
/// and new state (plus the change timestamp and timer snapshot), an invalid edge is rejected as
/// ProblemDetails, and the endpoint answers only an authenticated Operator assigned to the session.
/// </summary>
[Collection(PostgreSqlCollection.Name)]
public sealed class TransitionSessionStateEndpointTests : IAsyncLifetime
{
    private const int OperatorUserId = 42;
    private const string OperatorExternalIdentityId = "7909a3df-6e67-48dd-adf1-2a528e586fa0";

    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public TransitionSessionStateEndpointTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new SessionOperationsApiWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
        _factory.AuthenticatedActorProfileAccessClient.CurrentActor = new AuthenticatedActorProfileLookupDto(
            OperatorUserId,
            OperatorExternalIdentityId,
            "Operator",
            true);
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Transition_WithAssignedOperator_MovesSessionAndPersists()
    {
        var liveSession = await SeedSessionAsync();
        AddTrustedHeaders(_client, OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId:D}/state",
            new { targetState = "Preparing", reason = "Doors open" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<TransitionSessionStateResponse>();
        payload.Should().NotBeNull();
        payload!.LiveSessionId.Should().Be(liveSession.LiveSessionId);
        payload.PreviousState.Should().Be(nameof(SessionState.Scheduled));
        payload.CurrentState.Should().Be(nameof(SessionState.Preparing));
        payload.TransitionedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        payload.Timer.Should().NotBeNull();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await dbContext.LiveSessions.SingleAsync(s => s.LiveSessionId == liveSession.LiveSessionId);
        persisted.State.Should().Be(SessionState.Preparing);
    }

    [Fact]
    public async Task Transition_WithAssignedOperator_PersistsSessionEventWithActorAndReason()
    {
        var liveSession = await SeedSessionAsync();
        AddTrustedHeaders(_client, OperatorExternalIdentityId, "Operator", "operator@example.com");

        var reason = "Doors open";
        var response = await _client.PatchAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId:D}/state",
            new { targetState = "Preparing", reason });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reloaded = await dbContext.LiveSessions
            .Include(s => s.SessionEvents)
            .AsNoTracking()
            .SingleAsync(s => s.LiveSessionId == liveSession.LiveSessionId);

        reloaded.SessionEvents.Should().ContainSingle();
        var sessionEvent = reloaded.SessionEvents.Single();
        sessionEvent.EventType.Should().Be("SessionStateChanged");
        sessionEvent.ActorType.Should().Be(SessionEventActorType.Operator);
        sessionEvent.ActorId.Should().Be(OperatorUserId);
        sessionEvent.PayloadSummary.Should().Be($"Scheduled→Preparing: {reason}");
    }

    [Fact]
    public async Task Transition_WithStructurallyInvalidTarget_ReturnsConflict()
    {
        var liveSession = await SeedSessionAsync();
        AddTrustedHeaders(_client, OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId:D}/state",
            new { targetState = "Finished", reason = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.Should().Be("invalid-state-transition");
    }

    // Issue-1 fix: Finished is reached only via SessionCompletion, never a manual Operator PATCH —
    // even from Active, where the automatic path is legitimately allowed to land.
    [Fact]
    public async Task Transition_FromActiveToFinished_ReturnsConflict()
    {
        var liveSession = await SeedSessionAsync(
            mutate: session =>
            {
                var policy = new SessionStateTransitionPolicy();
                session.MoveTo(SessionState.Preparing, DateTimeOffset.UtcNow, policy);
                session.MoveTo(SessionState.Active, DateTimeOffset.UtcNow, policy);
            });
        AddTrustedHeaders(_client, OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId:D}/state",
            new { targetState = "Finished", reason = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.Should().Be("invalid-state-transition");
        // Issue-3 fix: the specific rejected edge is now surfaced, not a generic message.
        problem.Detail.Should().Be("Session cannot transition from 'Active' to 'Finished'.");
    }

    [Fact]
    public async Task Transition_ToActiveWithoutTeams_ReturnsConflict()
    {
        var liveSession = await SeedSessionAsync(
            registerTeam: false,
            mutate: session => session.MoveTo(SessionState.Preparing, DateTimeOffset.UtcNow, new SessionStateTransitionPolicy()));
        AddTrustedHeaders(_client, OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId:D}/state",
            new { targetState = "Active", reason = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.Should().Be("session-no-teams");
    }

    [Fact]
    public async Task Transition_WithUnknownState_ReturnsBadRequest()
    {
        var liveSession = await SeedSessionAsync();
        AddTrustedHeaders(_client, OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId:D}/state",
            new { targetState = "Teleporting", reason = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Transition_WithoutTrustedHeaders_ReturnsUnauthorized()
    {
        var liveSession = await SeedSessionAsync();

        var response = await _client.PatchAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId:D}/state",
            new { targetState = "Preparing", reason = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Locks the Operator role policy on the endpoint itself: an authenticated non-Operator is rejected
    // by the standard [Authorize(Policy = Operator)] guard before the request ever reaches the handler.
    [Fact]
    public async Task Transition_WithNonOperatorRole_ReturnsForbidden()
    {
        var liveSession = await SeedSessionAsync();
        AddTrustedHeaders(_client, OperatorExternalIdentityId, "Administrator", "admin@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId:D}/state",
            new { targetState = "Preparing", reason = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Transition_WithNonAssignedOperator_ReturnsForbidden()
    {
        var liveSession = await SeedSessionAsync();
        _factory.AuthenticatedActorProfileAccessClient.CurrentActor = new AuthenticatedActorProfileLookupDto(
            77,
            "kc-operator-77",
            "Operator",
            true);
        AddTrustedHeaders(_client, "kc-operator-77", "Operator", "intruder@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/sessions/{liveSession.LiveSessionId:D}/state",
            new { targetState = "Preparing", reason = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Transition_WithUnknownSession_ReturnsNotFound()
    {
        AddTrustedHeaders(_client, OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/sessions/{Guid.NewGuid():D}/state",
            new { targetState = "Preparing", reason = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<LiveSession> SeedSessionAsync(bool registerTeam = true, Action<LiveSession>? mutate = null)
    {
        var sourceMissionId = Guid.NewGuid();
        var liveSession = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Lifecycle Session",
            45,
            DateTimeOffset.UtcNow.AddHours(2),
            CreateTreasureHuntSnapshot(sourceMissionId));

        if (registerTeam)
        {
            liveSession.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);
        }

        liveSession.AssignOperator(OperatorUserId, DateTimeOffset.UtcNow.AddMinutes(-10));
        mutate?.Invoke(liveSession);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync();

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

    private static void AddTrustedHeaders(HttpClient client, string userId, string role, string email)
    {
        client.DefaultRequestHeaders.Remove("X-User-Id");
        client.DefaultRequestHeaders.Remove("X-User-Role");
        client.DefaultRequestHeaders.Remove("X-User-Email");
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-User-Role", role);
        client.DefaultRequestHeaders.Add("X-User-Email", email);
    }

    private sealed record TransitionSessionStateResponse(
        Guid LiveSessionId,
        string PreviousState,
        string CurrentState,
        DateTimeOffset TransitionedAt,
        SessionTimerSnapshotDto? Timer);
}
