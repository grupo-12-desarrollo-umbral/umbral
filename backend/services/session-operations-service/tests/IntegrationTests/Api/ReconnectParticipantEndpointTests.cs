using Microsoft.AspNetCore.Mvc;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class ReconnectParticipantEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public ReconnectParticipantEndpointTests(PostgreSqlFixture fixture)
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
    public async Task Reconnect_WithAuthorizedDisconnectedParticipant_RestoresLiveContext()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedSessionWithDisconnectedParticipantAsync(externalIdentityId, SessionState.Active);

        _factory.AccessClient.IsAllowed = true;
        AddTrustedHeaders(_client, externalIdentityId.ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/participants/reconnect",
            new { teamId = seeded.TeamId, displayName = "Nova", token = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ReconnectParticipantResponse>();
        payload.Should().NotBeNull();
        payload!.LiveSessionId.Should().Be(seeded.LiveSessionId);
        payload.TeamId.Should().Be(seeded.TeamId);
        payload.TeamDisplayName.Should().Be("Red");
        payload.SessionParticipantId.Should().Be(seeded.SessionParticipantId);
        payload.IsReconnect.Should().BeTrue();
        payload.SessionState.Should().Be(nameof(SessionState.Active));

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedSession = await dbContext.LiveSessions
            .Include(session => session.Participants)
            .SingleAsync(session => session.LiveSessionId == seeded.LiveSessionId);
        var participant = persistedSession.Participants
            .Single(p => p.SessionParticipantId == seeded.SessionParticipantId);
        participant.IsDisconnected.Should().BeFalse();
        participant.ParticipantStatus.Should().Be(ParticipantStatus.Active);
    }

    [Fact]
    public async Task Reconnect_WithoutTrustedHeaders_ReturnsUnauthorized()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedSessionWithDisconnectedParticipantAsync(externalIdentityId, SessionState.Active);

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/participants/reconnect",
            new { teamId = seeded.TeamId, displayName = "Nova", token = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reconnect_WithOperatorRole_ReturnsForbidden()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedSessionWithDisconnectedParticipantAsync(externalIdentityId, SessionState.Active);

        AddTrustedHeaders(_client, externalIdentityId.ToString(), "Operator", "operator@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/participants/reconnect",
            new { teamId = seeded.TeamId, displayName = "Nova", token = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reconnect_WhenAccessFactDenied_ReturnsForbidden()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedSessionWithDisconnectedParticipantAsync(externalIdentityId, SessionState.Active);

        _factory.AccessClient.IsAllowed = false;
        AddTrustedHeaders(_client, externalIdentityId.ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/participants/reconnect",
            new { teamId = seeded.TeamId, displayName = "Nova", token = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("Forbidden.");
    }

    [Fact]
    public async Task Reconnect_FirstJoinIntoActiveSession_ReturnsForbiddenLateJoin()
    {
        var seeded = await SeedSessionWithoutParticipantAsync(SessionState.Active);
        var newParticipantIdentity = Guid.NewGuid();

        AddTrustedHeaders(_client, newParticipantIdentity.ToString(), "Participant", "newcomer@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/participants/reconnect",
            new { teamId = seeded.TeamId, displayName = "Newcomer", token = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Reconnect_IntoFinishedSession_ReturnsForbiddenInvalidState()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedSessionWithDisconnectedParticipantAsync(externalIdentityId, SessionState.Finished);

        AddTrustedHeaders(_client, externalIdentityId.ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/participants/reconnect",
            new { teamId = seeded.TeamId, displayName = "Nova", token = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reconnect_ToWrongTeam_ReturnsConflict()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedSessionWithDisconnectedParticipantAsync(externalIdentityId, SessionState.Active, registerSecondTeam: true);

        AddTrustedHeaders(_client, externalIdentityId.ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/participants/reconnect",
            new { teamId = seeded.OtherTeamId, displayName = "Nova", token = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Reconnect_FirstJoinWhenTeamFull_ReturnsConflict()
    {
        var seeded = await SeedSessionWithoutParticipantAsync(SessionState.Scheduled);
        var newParticipantIdentity = Guid.NewGuid();

        AddTrustedHeaders(_client, newParticipantIdentity.ToString(), "Participant", "newcomer@example.com");

        // First participant fills the single open seat (capacity 1).
        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/participants/reconnect",
            new { teamId = seeded.TeamId, displayName = "Newcomer", token = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // A second, different participant now exceeds capacity -> conflict.

        var secondIdentity = Guid.NewGuid();
        AddTrustedHeaders(_client, secondIdentity.ToString(), "Participant", "second@example.com");

        var conflictResponse = await _client.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/participants/reconnect",
            new { teamId = seeded.TeamId, displayName = "Second", token = (string?)null });

        conflictResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Reconnect_WhenSessionDoesNotExist_ReturnsNotFound()
    {
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{Guid.NewGuid()}/participants/reconnect",
            new { teamId = Guid.NewGuid(), displayName = "Nova", token = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reconnect_WithBlankDisplayName_ReturnsBadRequest()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedSessionWithDisconnectedParticipantAsync(externalIdentityId, SessionState.Active);

        AddTrustedHeaders(_client, externalIdentityId.ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/participants/reconnect",
            new { teamId = seeded.TeamId, displayName = " ", token = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Validation failed.");
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

    private async Task<SeededSession> SeedSessionWithDisconnectedParticipantAsync(
        Guid externalIdentityId,
        SessionState state,
        bool registerSecondTeam = false)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        var liveSession = CreateSession(scheduledAt);
        var team = liveSession.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", registerSecondTeam ? 4 : 1);
        Guid? otherTeamId = null;
        if (registerSecondTeam)
        {
            otherTeamId = liveSession.AssociateTeam(Guid.NewGuid(), "Blue", "BLUE-01", 4).TeamId;
        }

        var admission = liveSession.AdmitParticipant(
            externalIdentityId,
            "Nova",
            team.TeamId,
            scheduledAt.AddMinutes(1),
            new JoinPolicy());

        liveSession.DisconnectParticipant(admission.Participant.SessionParticipantId, scheduledAt.AddMinutes(5));
        MoveToState(liveSession, state, scheduledAt.AddMinutes(6));

        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync();

        return new SeededSession(
            liveSession.LiveSessionId,
            team.TeamId,
            otherTeamId ?? Guid.Empty,
            admission.Participant.SessionParticipantId);
    }

    private async Task<SeededSession> SeedSessionWithoutParticipantAsync(SessionState state)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        var liveSession = CreateSession(scheduledAt);
        var team = liveSession.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 1);
        MoveToState(liveSession, state, scheduledAt.AddMinutes(1));

        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync();

        return new SeededSession(liveSession.LiveSessionId, team.TeamId, Guid.Empty, Guid.Empty);
    }

    private static void MoveToState(LiveSession liveSession, SessionState state, DateTimeOffset occurredAt)
    {
        var transitionPolicy = new SessionStateTransitionPolicy();
        switch (state)
        {
            case SessionState.Scheduled:
                break;
            case SessionState.Active:
                liveSession.MoveTo(SessionState.Preparing, occurredAt, transitionPolicy);
                liveSession.MoveTo(SessionState.Active, occurredAt.AddMinutes(1), transitionPolicy);
                break;
            case SessionState.Finished:
                liveSession.MoveTo(SessionState.Preparing, occurredAt, transitionPolicy);
                liveSession.MoveTo(SessionState.Active, occurredAt.AddMinutes(1), transitionPolicy);
                liveSession.MoveTo(SessionState.Finished, occurredAt.AddMinutes(2), transitionPolicy);
                break;
            case SessionState.Cancelled:
                liveSession.MoveTo(SessionState.Cancelled, occurredAt, transitionPolicy);
                break;
            default:
                liveSession.MoveTo(state, occurredAt, transitionPolicy);
                break;
        }
    }

    private static LiveSession CreateSession(DateTimeOffset scheduledAt)
    {
        return LiveSession.Create(
            SessionMode.TreasureHunt,
            SessionSource.Create(SessionSourceType.Mission, Guid.NewGuid()),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Reconnect Session",
            45,
            scheduledAt);
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

    private sealed record SeededSession(
        Guid LiveSessionId,
        Guid TeamId,
        Guid OtherTeamId,
        Guid SessionParticipantId);

    private sealed record ReconnectParticipantResponse(
        Guid LiveSessionId,
        Guid TeamId,
        string TeamDisplayName,
        Guid SessionParticipantId,
        string ParticipantDisplayName,
        string SessionState,
        bool IsReconnect,
        DateTimeOffset JoinedAt,
        DateTimeOffset LastSeenAt);

    private sealed record HealthStatusResponse(string Status);
}
