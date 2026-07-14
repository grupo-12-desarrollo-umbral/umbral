using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

// GET {id}/clues/releasable — the operator's release-clue picker source. Returns only the active
// active substage's hidden clues, target-backed for treasure hunt and clue-snapshot-backed for trivia.
[Collection(PostgreSqlCollection.Name)]
public sealed class ReleasableCluesEndpointTests : IAsyncLifetime
{
    private const int OwnerOperatorUserId = 42;
    private const string OwnerOperatorExternalIdentityId = "kc-operator-42";
    private const int OtherOperatorUserId = 99;
    private const string OtherOperatorExternalIdentityId = "kc-operator-99";

    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public ReleasableCluesEndpointTests(PostgreSqlFixture fixture)
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
    public async Task GetReleasableClues_AsAssignedOperator_ReturnsOnlyHiddenClueTargetsOfActiveSubstage()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        SetCurrentActor(OwnerOperatorUserId, OwnerOperatorExternalIdentityId);
        AddTrustedHeaders(_client, OwnerOperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await _client.GetAsync(BuildUrl(seeded.LiveSessionId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ReleasableCluesResponse>();
        payload.Should().NotBeNull();
        payload!.LiveSessionId.Should().Be(seeded.LiveSessionId);
        payload.ActiveSubstageId.Should().Be(seeded.ActiveSubstageId);
        // Only the HiddenUntilOperatorRelease target is releasable; the always-visible one is excluded.
        payload.Clues.Should().ContainSingle();
        payload.Clues[0].Should().Be(new ReleasableClueResponse(
            seeded.HiddenTargetId,
            null,
            "Target Bravo",
            2,
            "Look behind the painting."));
    }

    [Fact]
    public async Task GetReleasableClues_Trivia_ReturnsHiddenSubstageClue()
    {
        var seeded = await SeedActiveTriviaSessionAsync();
        SetCurrentActor(OwnerOperatorUserId, OwnerOperatorExternalIdentityId);
        AddTrustedHeaders(_client, OwnerOperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await _client.GetAsync(BuildUrl(seeded.LiveSessionId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ReleasableCluesResponse>();
        payload.Should().NotBeNull();
        payload!.Clues.Should().ContainSingle();
        payload.Clues[0].Should().Be(new ReleasableClueResponse(
            null,
            seeded.HiddenClueId,
            null,
            2,
            "Operator-only trivia clue."));
    }

    [Fact]
    public async Task GetReleasableClues_AsNonOwningOperator_ReturnsForbiddenProblemDetails()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        SetCurrentActor(OtherOperatorUserId, OtherOperatorExternalIdentityId);
        AddTrustedHeaders(_client, OtherOperatorExternalIdentityId, "Operator", "other-operator@example.com");

        var response = await _client.GetAsync(BuildUrl(seeded.LiveSessionId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Forbidden);
        problem.Type.Should().Be("forbidden-access");
    }

    private async Task<SeededTriviaSession> SeedActiveTriviaSessionAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var sourceMissionId = Guid.NewGuid();
        var snapshot = CreateTriviaSnapshot(sourceMissionId);
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"TRV-{Guid.NewGuid():N}"[..12],
            "Releasable Trivia Clues",
            20,
            createdAt,
            snapshot);
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "ALP-01", 4);
        session.AssignOperator(OwnerOperatorUserId, createdAt);

        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(1), transitionPolicy);
        session.MoveTo(SessionState.Active, createdAt.AddMinutes(2), transitionPolicy);

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededTriviaSession(
            session.LiveSessionId,
            snapshot.ClueSnapshots.Single(clue => clue.IsHiddenUntilOperatorRelease).ClueSnapshotId);
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
            $"HNT-{Guid.NewGuid():N}"[..12],
            "Releasable Targets Hunt",
            maximumTimeMinutes: 45,
            createdAt,
            CreateTreasureHuntSnapshot(sourceMissionId));

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

        return new SeededSession(
            session.LiveSessionId,
            session.ActiveSubstageId,
            orderedTargets[0].TargetSnapshotId,
            orderedTargets[1].TargetSnapshotId);
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(Guid sourceMissionId)
    {
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Releasable Targets Hunt",
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

    private static MissionRuntimeSnapshot CreateTriviaSnapshot(Guid sourceMissionId)
    {
        var substage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);
        var question = TriviaQuestionSnapshot.Create(
            substage.SubstageSnapshotId,
            "Which planet is closest to the Sun?",
            1,
            100,
            30,
            "Mercury is closest.",
            [
                TriviaOptionSnapshot.Create("Mercury", 1, true),
                TriviaOptionSnapshot.Create("Venus", 2, false)
            ]);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Releasable Trivia Clues",
            MaximumTime.Create(20),
            [StageSnapshot.Create("Stage One", 1, [substage])],
            [],
            [question],
            [
                ClueSnapshot.Create(
                    substage.SubstageSnapshotId,
                    "Visible trivia clue.",
                    ClueSnapshot.VisibleWhenSubstageStartsPolicy,
                    1),
                ClueSnapshot.Create(
                    substage.SubstageSnapshotId,
                    "Operator-only trivia clue.",
                    ClueSnapshot.HiddenUntilOperatorReleasePolicy,
                    2)
            ]);
    }

    private void SetCurrentActor(int userId, string externalIdentityId)
    {
        _factory.AuthenticatedActorProfileAccessClient.CurrentActor = new AuthenticatedActorProfileLookupDto(
            userId,
            externalIdentityId,
            "Operator",
            true);
    }

    private static string BuildUrl(Guid liveSessionId)
    {
        return $"/api/sessions/{liveSessionId:D}/clues/releasable";
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
        Guid? ActiveSubstageId,
        Guid VisibleTargetId,
        Guid HiddenTargetId);

    private sealed record SeededTriviaSession(Guid LiveSessionId, Guid HiddenClueId);

    private sealed record ReleasableCluesResponse(
        Guid LiveSessionId,
        Guid? ActiveSubstageId,
        IReadOnlyList<ReleasableClueResponse> Clues);

    private sealed record ReleasableClueResponse(
        Guid? TargetId,
        Guid? ClueId,
        string? TargetName,
        int SequenceOrder,
        string ClueText);

    private sealed record ProblemDetailsResponse(string? Type, string? Title, int? Status, string? Detail);
}
