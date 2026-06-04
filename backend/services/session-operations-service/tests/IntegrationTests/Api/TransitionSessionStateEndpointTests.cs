using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class TransitionSessionStateEndpointTests : IAsyncLifetime
{
    private const int OperatorUserId = 42;
    private const string OperatorExternalIdentityId = "kc-operator-42";

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
        payload!.PreviousState.Should().Be(nameof(SessionState.Scheduled));
        payload.CurrentState.Should().Be(nameof(SessionState.Preparing));

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await dbContext.LiveSessions.SingleAsync(s => s.LiveSessionId == liveSession.LiveSessionId);
        persisted.State.Should().Be(SessionState.Preparing);
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
        var liveSession = LiveSession.Create(
            SessionMode.TreasureHunt,
            SessionSource.Create(SessionSourceType.Mission, Guid.NewGuid()),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Lifecycle Session",
            45,
            DateTimeOffset.UtcNow.AddHours(2));

        if (registerTeam)
        {
            liveSession.RegisterTeam("Red", "RED-01", 4);
        }

        liveSession.AssignOperator(OperatorUserId, DateTimeOffset.UtcNow.AddMinutes(-10));
        mutate?.Invoke(liveSession);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync();

        return liveSession;
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
        DateTimeOffset TransitionedAt);
}
