using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

// HU-22: the operator timer GET returns the active-substage (trivia-question) window. With an
// active question it advances/freezes/resumes on that window; with no active question (or a
// treasure-hunt substage) there is no authoritative countdown (OD-1) -> 0/absent, ActiveQuestion null.
[Collection(PostgreSqlCollection.Name)]
public sealed class OperatorSessionTimerSnapshotEndpointTests : IAsyncLifetime
{
    private const int OperatorUserId = 42;
    private const string OperatorExternalIdentityId = "kc-operator-42";
    private const int QuestionTimeLimitSeconds = 300;

    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public OperatorSessionTimerSnapshotEndpointTests(PostgreSqlFixture fixture)
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
    public async Task GetTimerSnapshot_ForActiveTriviaQuestion_ReturnsAdvancingQuestionWindow()
    {
        var seeded = await SeedTriviaSessionAsync(SessionTimerSeedState.ActiveQuestion);
        AddTrustedHeaders(_client, OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await _client.GetAsync(BuildTimerUrl(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<OperatorSessionTimerSnapshotResponse>();
        payload.Should().NotBeNull();
        payload!.LiveSessionId.Should().Be(seeded);
        payload.TeamId.Should().BeNull();
        payload.SessionState.Should().Be(nameof(SessionState.Active));
        payload.TotalSeconds.Should().Be(QuestionTimeLimitSeconds);
        payload.RemainingSeconds.Should().BeInRange(QuestionTimeLimitSeconds - 10, QuestionTimeLimitSeconds);
        payload.TimerStatus.Should().Be("Advancing");
        payload.IsAdvancing.Should().BeTrue();
        payload.IsExpired.Should().BeFalse();
        payload.AdvancingSince.Should().NotBeNull();
        payload.ExpiredAt.Should().BeNull();
        payload.ActiveQuestion.Should().NotBeNull();
        payload.ActiveQuestion!.QuestionIndex.Should().Be(0);
        payload.ActiveQuestion.TimeLimitSeconds.Should().Be(QuestionTimeLimitSeconds);
    }

    [Fact]
    public async Task GetTimerSnapshot_ForPausedTriviaQuestion_ReturnsFrozenQuestionWindow()
    {
        var seeded = await SeedTriviaSessionAsync(SessionTimerSeedState.PausedQuestion);
        AddTrustedHeaders(_client, OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await _client.GetAsync(BuildTimerUrl(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<OperatorSessionTimerSnapshotResponse>();
        payload.Should().NotBeNull();
        payload!.SessionState.Should().Be(nameof(SessionState.Paused));
        payload.TotalSeconds.Should().Be(QuestionTimeLimitSeconds);
        payload.RemainingSeconds.Should().Be(180);
        payload.TimerStatus.Should().Be("Frozen");
        payload.IsAdvancing.Should().BeFalse();
        payload.IsExpired.Should().BeFalse();
        payload.AdvancingSince.Should().BeNull();
        payload.ActiveQuestion.Should().NotBeNull();
        payload.ActiveQuestion!.RemainingSeconds.Should().Be(180);
    }

    [Fact]
    public async Task GetTimerSnapshot_ForResumedTriviaQuestion_ContinuesSameQuestionFromFrozenRemainder()
    {
        var seeded = await SeedTriviaSessionAsync(SessionTimerSeedState.ResumedQuestion);
        AddTrustedHeaders(_client, OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await _client.GetAsync(BuildTimerUrl(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<OperatorSessionTimerSnapshotResponse>();
        payload.Should().NotBeNull();
        payload!.SessionState.Should().Be(nameof(SessionState.Active));
        payload.TotalSeconds.Should().Be(QuestionTimeLimitSeconds);
        payload.RemainingSeconds.Should().BeInRange(170, 180);
        payload.TimerStatus.Should().Be("Advancing");
        payload.IsAdvancing.Should().BeTrue();
        payload.IsExpired.Should().BeFalse();
        payload.ActiveQuestion.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTimerSnapshot_ForActiveTreasureHuntSession_ReturnsNoCountdown()
    {
        var seeded = await SeedTreasureHuntSessionAsync();
        AddTrustedHeaders(_client, OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await _client.GetAsync(BuildTimerUrl(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<OperatorSessionTimerSnapshotResponse>();
        payload.Should().NotBeNull();
        payload!.LiveSessionId.Should().Be(seeded);
        payload.TeamId.Should().BeNull();
        payload.SessionState.Should().Be(nameof(SessionState.Active));
        payload.TotalSeconds.Should().Be(0);
        payload.RemainingSeconds.Should().Be(0);
        payload.IsAdvancing.Should().BeFalse();
        payload.ActiveQuestion.Should().BeNull();
    }

    [Fact]
    public async Task GetTimerSnapshot_WhenSessionDoesNotExist_ReturnsNotFound()
    {
        AddTrustedHeaders(_client, OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await _client.GetAsync($"/api/sessions/{Guid.NewGuid():D}/timer");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTimerSnapshot_WithoutTrustedHeaders_ReturnsUnauthorized()
    {
        var seeded = await SeedTriviaSessionAsync(SessionTimerSeedState.ActiveQuestion);

        var response = await _client.GetAsync(BuildTimerUrl(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTimerSnapshot_WithParticipantRole_ReturnsForbidden()
    {
        var seeded = await SeedTriviaSessionAsync(SessionTimerSeedState.ActiveQuestion);
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.GetAsync(BuildTimerUrl(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<Guid> SeedTriviaSessionAsync(SessionTimerSeedState seedState)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTimeOffset.UtcNow;
        var createdAt = now.AddMinutes(-20);
        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"TRV-{Guid.NewGuid():N}"[..12],
            "Operator Timer Trivia",
            maximumTimeMinutes: 45,
            createdAt,
            CreateTriviaSnapshot(sourceMissionId));

        session.AssociateTeam(Guid.NewGuid(), "Blue", "BLU-01", 4);
        session.AssignOperator(OperatorUserId, createdAt);

        DriveQuestionTimer(session, seedState, createdAt, now);

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return session.LiveSessionId;
    }

    private async Task<Guid> SeedTreasureHuntSessionAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"HNT-{Guid.NewGuid():N}"[..12],
            "Operator Timer Hunt",
            maximumTimeMinutes: 45,
            createdAt,
            CreateTreasureHuntSnapshot(sourceMissionId));

        session.AssociateTeam(Guid.NewGuid(), "Blue", "BLU-01", 4);
        session.AssignOperator(OperatorUserId, createdAt);

        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(1), transitionPolicy);
        session.MoveTo(SessionState.Active, createdAt.AddMinutes(2), transitionPolicy);

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return session.LiveSessionId;
    }

    // Drives the trivia-question timer to the requested state. Advancing windows anchor on the real
    // "now" so the endpoint (real clock) reads a meaningful remainder; frozen windows are fully
    // seed-timed so the frozen remainder is deterministic.
    private static void DriveQuestionTimer(
        LiveSession session,
        SessionTimerSeedState seedState,
        DateTimeOffset createdAt,
        DateTimeOffset now)
    {
        var transitionPolicy = new SessionStateTransitionPolicy();

        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(1), transitionPolicy);
        session.MoveTo(SessionState.Active, createdAt.AddMinutes(2), transitionPolicy);

        if (seedState == SessionTimerSeedState.ActiveQuestion)
        {
            session.ActivateQuestion(0, now);
            return;
        }

        // Paused / Resumed: activate then pause 120s later -> frozen remainder = 300 - 120 = 180.
        var activatedAt = createdAt.AddMinutes(3);
        session.ActivateQuestion(0, activatedAt);
        session.MoveTo(SessionState.Paused, activatedAt.AddSeconds(120), transitionPolicy, "Pause timer");

        if (seedState == SessionTimerSeedState.PausedQuestion)
        {
            return;
        }

        session.MoveTo(SessionState.Active, now, transitionPolicy);
    }

    private static MissionRuntimeSnapshot CreateTriviaSnapshot(Guid sourceMissionId)
    {
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Operator Timer Quiz",
            MaximumTime.Create(45),
            [
                StageSnapshot.Create("Stage One", 1, [triviaSubstage])
            ],
            [],
            [
                TriviaQuestionSnapshot.Create(
                    triviaSubstage.SubstageSnapshotId,
                    "What is the closest planet to the Sun?",
                    1,
                    100,
                    QuestionTimeLimitSeconds,
                    "Mercury is the closest planet.",
                    [
                        TriviaOptionSnapshot.Create("Mercury", 1, true),
                        TriviaOptionSnapshot.Create("Venus", 2, false)
                    ])
            ]);
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(Guid sourceMissionId)
    {
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Operator Timer Hunt",
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
                    "Look under the stairs",
                    "AfterPreviousTarget")
            ],
            []);
    }

    private static string BuildTimerUrl(Guid liveSessionId)
    {
        return $"/api/sessions/{liveSessionId:D}/timer";
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
        ActiveQuestion,
        PausedQuestion,
        ResumedQuestion
    }

    private sealed record OperatorSessionTimerSnapshotResponse(
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
        DateTimeOffset? ExpiredAt,
        ActiveQuestionSnapshotResponse? ActiveQuestion);

    private sealed record ActiveQuestionSnapshotResponse(
        Guid LiveSessionId,
        int QuestionIndex,
        int SequenceOrder,
        string Prompt,
        IReadOnlyList<string> Options,
        int TimeLimitSeconds,
        int RemainingSeconds,
        DateTimeOffset ActivatedAt);
}
