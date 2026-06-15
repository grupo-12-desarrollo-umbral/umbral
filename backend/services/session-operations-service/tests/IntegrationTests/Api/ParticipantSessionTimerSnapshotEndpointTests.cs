using Microsoft.AspNetCore.Mvc;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class ParticipantSessionTimerSnapshotEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public ParticipantSessionTimerSnapshotEndpointTests(PostgreSqlFixture fixture)
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
    public async Task GetTimerSnapshot_ForActiveTriviaSession_ReturnsAuthoritativeAdvancingTimer()
    {
        var seeded = await SeedTriviaSessionAsync(SessionTimerSeedState.Active);
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.GetAsync(BuildTimerUrl(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<SessionTimerSnapshotResponse>();
        payload.Should().NotBeNull();
        payload!.LiveSessionId.Should().Be(seeded.LiveSessionId);
        payload.TeamId.Should().Be(seeded.TeamId);
        payload.SessionState.Should().Be(nameof(SessionState.Active));
        payload.TotalSeconds.Should().Be(2700);
        payload.RemainingSeconds.Should().BeInRange(1610, 1630);
        payload.TimerStatus.Should().Be("Advancing");
        payload.IsAdvancing.Should().BeTrue();
        payload.IsExpired.Should().BeFalse();
        payload.AdvancingSince.Should().NotBeNull();
        payload.ExpiredAt.Should().BeNull();
    }

    [Fact]
    public async Task GetTimerSnapshot_ForPausedTriviaSession_ReturnsFrozenAuthoritativeTimer()
    {
        var seeded = await SeedTriviaSessionAsync(SessionTimerSeedState.Paused);
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.GetAsync(BuildTimerUrl(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<SessionTimerSnapshotResponse>();
        payload.Should().NotBeNull();
        payload!.SessionState.Should().Be(nameof(SessionState.Paused));
        payload.RemainingSeconds.Should().Be(2580);
        payload.TimerStatus.Should().Be("Frozen");
        payload.IsAdvancing.Should().BeFalse();
        payload.IsExpired.Should().BeFalse();
        payload.AdvancingSince.Should().BeNull();
    }

    [Fact]
    public async Task GetTimerSnapshot_ForResumedTriviaSession_ContinuesFromFrozenRemainder()
    {
        var seeded = await SeedTriviaSessionAsync(SessionTimerSeedState.Resumed);
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.GetAsync(BuildTimerUrl(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<SessionTimerSnapshotResponse>();
        payload.Should().NotBeNull();
        payload!.SessionState.Should().Be(nameof(SessionState.Active));
        payload.TotalSeconds.Should().Be(2700);
        payload.RemainingSeconds.Should().BeInRange(2210, 2230);
        payload.TimerStatus.Should().Be("Advancing");
        payload.IsAdvancing.Should().BeTrue();
        payload.IsExpired.Should().BeFalse();
    }

    [Fact]
    public async Task GetTimerSnapshot_WhenSessionDoesNotExist_ReturnsNotFound()
    {
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.GetAsync(
            $"/api/sessions/{Guid.NewGuid():D}/participants/timer?teamId={Guid.NewGuid():D}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTimerSnapshot_WhenAccessFactDenied_ReturnsForbidden()
    {
        var seeded = await SeedTriviaSessionAsync(SessionTimerSeedState.Active);
        _factory.AccessClient.IsAllowed = false;
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.GetAsync(BuildTimerUrl(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task GetTimerSnapshot_WithoutTrustedHeaders_ReturnsUnauthorized()
    {
        var seeded = await SeedTriviaSessionAsync(SessionTimerSeedState.Active);

        var response = await _client.GetAsync(BuildTimerUrl(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTimerSnapshot_WithOperatorRole_ReturnsForbidden()
    {
        var seeded = await SeedTriviaSessionAsync(SessionTimerSeedState.Active);
        AddTrustedHeaders(_client, "kc-operator-1", "Operator", "operator@example.com");

        var response = await _client.GetAsync(BuildTimerUrl(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<SeededSession> SeedTriviaSessionAsync(SessionTimerSeedState seedState)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var session = LiveSession.CreateTrivia(
            SessionSource.CreateTriviaQuiz(42),
            $"TRV-{Guid.NewGuid():N}"[..12],
            "Authoritative Timer Trivia",
            maximumTimeMinutes: 45,
            createdAt,
            CreateTriviaSnapshot());
        var team = session.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);

        MoveToRequestedTimerState(session, seedState, createdAt);

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(session.LiveSessionId, team.TeamId);
    }

    private static void MoveToRequestedTimerState(
        LiveSession session,
        SessionTimerSeedState seedState,
        DateTimeOffset createdAt)
    {
        var transitionPolicy = new SessionStateTransitionPolicy();

        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(1), transitionPolicy);
        session.MoveTo(SessionState.Active, createdAt.AddMinutes(2), transitionPolicy);

        if (seedState == SessionTimerSeedState.Active)
        {
            return;
        }

        session.MoveTo(SessionState.Paused, createdAt.AddMinutes(4), transitionPolicy, "Pause timer");

        if (seedState == SessionTimerSeedState.Paused)
        {
            return;
        }

        session.MoveTo(SessionState.Active, createdAt.AddMinutes(14), transitionPolicy);
    }

    private static TriviaSessionSnapshot CreateTriviaSnapshot()
    {
        return TriviaSessionSnapshot.Create(
            "Timer Quiz",
            [
                TriviaQuestionSnapshot.Create(
                    "What is the closest planet to the Sun?",
                    1,
                    100,
                    30,
                    "Mercury is the closest planet.",
                    [
                        TriviaOptionSnapshot.Create("Mercury", 1, true),
                        TriviaOptionSnapshot.Create("Venus", 2, false)
                    ])
            ]);
    }

    private static string BuildTimerUrl(SeededSession seeded)
    {
        return $"/api/sessions/{seeded.LiveSessionId:D}/participants/timer?teamId={seeded.TeamId:D}";
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

    private enum SessionTimerSeedState
    {
        Active,
        Paused,
        Resumed
    }

    private sealed record SeededSession(Guid LiveSessionId, Guid TeamId);

    private sealed record SessionTimerSnapshotResponse(
        Guid LiveSessionId,
        Guid? TeamId,
        string SessionState,
        int TotalSeconds,
        int RemainingSeconds,
        string TimerStatus,
        bool IsAdvancing,
        bool IsExpired,
        DateTimeOffset ObservedAt,
        DateTimeOffset? AdvancingSince,
        DateTimeOffset? ExpiredAt);
}
