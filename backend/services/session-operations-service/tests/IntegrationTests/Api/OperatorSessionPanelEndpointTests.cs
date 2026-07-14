using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class OperatorSessionPanelEndpointTests : IAsyncLifetime
{
    private const int OwnerOperatorUserId = 42;
    private const string OwnerOperatorExternalIdentityId = "kc-operator-42";
    private const int OtherOperatorUserId = 99;
    private const string OtherOperatorExternalIdentityId = "kc-operator-99";

    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public OperatorSessionPanelEndpointTests(PostgreSqlFixture fixture)
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
    public async Task GetOperatorPanel_AsAssignedOperator_ReturnsStateTimerAndOrderedTeamProgress()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        SetCurrentActor(OwnerOperatorUserId, OwnerOperatorExternalIdentityId);
        AddTrustedHeaders(_client, OwnerOperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await _client.GetAsync(BuildPanelUrl(seeded.LiveSessionId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<OperatorSessionPanelResponse>();
        payload.Should().NotBeNull();
        payload!.LiveSessionId.Should().Be(seeded.LiveSessionId);
        payload.State.Should().Be(nameof(SessionState.Active));
        payload.Timer.SessionState.Should().Be(nameof(SessionState.Active));
        payload.TeamProgress.Select(team => team.TeamCode).Should().Equal("ALP-01", "BRV-02");
        payload.TeamProgress.Select(team => team.Score).Should().OnlyContain(score => score == 0);
        payload.TeamProgress.Should().OnlyContain(team => team.ActiveSubstage != null);
        payload.TeamProgress.Select(team => team.ActiveSubstage!.PlayMode)
            .Should().OnlyContain(playMode => playMode == SubstagePlayMode.TreasureHunt.ToString());
        payload.TeamProgress.Select(team => team.ActiveSubstage!.TotalActiveTargets)
            .Should().OnlyContain(totalActiveTargets => totalActiveTargets == 2);
        payload.TeamProgress.Select(team => team.ActiveSubstage!.ResolvedTargets)
            .Should().OnlyContain(resolvedTargets => resolvedTargets == 0);
        payload.TeamProgress.Select(team => team.ActiveSubstage!.Targets).Should().OnlyContain(targets =>
            targets.Count == 2 &&
            targets[0] == new ActiveSubstageTargetResponse(seeded.VisibleTargetId, "Target Alpha", 1, false) &&
            targets[1] == new ActiveSubstageTargetResponse(seeded.HiddenTargetId, "Target Bravo", 2, true));
    }

    [Fact]
    public async Task GetOperatorPanel_AsNonOwningOperator_ReturnsForbiddenProblemDetails()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        SetCurrentActor(OtherOperatorUserId, OtherOperatorExternalIdentityId);
        AddTrustedHeaders(_client, OtherOperatorExternalIdentityId, "Operator", "other-operator@example.com");

        var response = await _client.GetAsync(BuildPanelUrl(seeded.LiveSessionId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Forbidden);
        problem.Type.Should().Be("forbidden-access");
    }

    private async Task<SeededPanelSession> SeedActiveTreasureHuntSessionAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTimeOffset.UtcNow;
        var createdAt = now.AddMinutes(-20);
        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"HNT-{Guid.NewGuid():N}"[..12],
            "Operator Session Panel Hunt",
            maximumTimeMinutes: 45,
            createdAt,
            CreateTreasureHuntSnapshot(sourceMissionId));

        session.AssociateTeam(Guid.NewGuid(), "Bravo", "BRV-02", 4);
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "ALP-01", 4);
        session.AssignOperator(OwnerOperatorUserId, createdAt);

        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(1), transitionPolicy);
        session.MoveTo(SessionState.Active, createdAt.AddMinutes(2), transitionPolicy);

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        var orderedTargets = session.MissionRuntimeSnapshot.TargetSnapshots
            .OrderBy(target => target.SequenceOrder)
            .ToArray();

        return new SeededPanelSession(
            session.LiveSessionId,
            orderedTargets[0].TargetSnapshotId,
            orderedTargets[1].TargetSnapshotId);
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(Guid sourceMissionId)
    {
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Operator Session Panel Hunt",
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
                    "Look under the stairs.",
                    "VisibleWhenSubstageStarts"),
                TargetSnapshot.Create(
                    treasureHuntSubstage.SubstageSnapshotId,
                    "Target Bravo",
                    "QR-BRAVO",
                    2,
                    true,
                    50,
                    4.712,
                    -74.073,
                    "Look behind the painting.",
                    "HiddenUntilOperatorRelease")
            ],
            []);
    }

    private void SetCurrentActor(int userId, string externalIdentityId)
    {
        _factory.AuthenticatedActorProfileAccessClient.CurrentActor = new AuthenticatedActorProfileLookupDto(
            userId,
            externalIdentityId,
            "Operator",
            true);
    }

    private static string BuildPanelUrl(Guid liveSessionId)
    {
        return $"/api/sessions/{liveSessionId:D}/operator-panel";
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

    private sealed record SeededPanelSession(
        Guid LiveSessionId,
        Guid VisibleTargetId,
        Guid HiddenTargetId);

    private sealed record OperatorSessionPanelResponse(
        Guid LiveSessionId,
        string State,
        SessionTimerSnapshotResponse Timer,
        IReadOnlyList<OperatorTeamProgressResponse> TeamProgress);

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

    private sealed record OperatorTeamProgressResponse(
        Guid TeamId,
        string TeamCode,
        string DisplayName,
        int Score,
        ActiveSubstageContextResponse? ActiveSubstage);

    private sealed record ActiveSubstageContextResponse(
        Guid SubstageSnapshotId,
        string PlayMode,
        string Title,
        int TotalActiveTargets,
        int ResolvedTargets,
        int? ActiveQuestionSequenceOrder,
        int? ActiveQuestionTimeLimitSeconds,
        IReadOnlyList<ActiveSubstageTargetResponse> Targets);

    private sealed record ActiveSubstageTargetResponse(
        Guid TargetSnapshotId,
        string Name,
        int SequenceOrder,
        bool HasHiddenClue);

    private sealed record ProblemDetailsResponse(string? Type, string? Title, int? Status, string? Detail);
}
