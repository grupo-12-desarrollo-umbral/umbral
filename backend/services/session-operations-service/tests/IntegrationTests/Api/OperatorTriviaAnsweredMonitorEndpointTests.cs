using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

// HU-36A: the operator answered-monitor GET returns per-team answered/not-answered for the active
// synchronized trivia question, gated to the assigned operator by the ownership resolver Proxy.
// It exposes the active-question identity (SubstageSnapshotId, QuestionSequenceOrder) but never the
// chosen option, correctness, or points (those are HU-35 / HU-36B). A non-owning operator gets 403.
[Collection(PostgreSqlCollection.Name)]
public sealed class OperatorTriviaAnsweredMonitorEndpointTests : IAsyncLifetime
{
    private const int OwnerOperatorUserId = 42;
    private const string OwnerOperatorExternalIdentityId = "kc-operator-42";
    private const int OtherOperatorUserId = 99;
    private const string OtherOperatorExternalIdentityId = "kc-operator-99";
    private const int QuestionTimeLimitSeconds = 300;
    private const int AnsweredOptionSequenceOrder = 1;

    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public OperatorTriviaAnsweredMonitorEndpointTests(PostgreSqlFixture fixture)
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
    public async Task GetAnsweredMonitor_AsAssignedOperator_ReturnsPerTeamAnsweredStatus()
    {
        var seeded = await SeedTriviaSessionWithOneTeamAnsweredAsync();
        SetCurrentActor(OwnerOperatorUserId, OwnerOperatorExternalIdentityId);
        AddTrustedHeaders(_client, OwnerOperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await _client.GetAsync(BuildMonitorUrl(seeded.LiveSessionId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<TriviaAnsweredMonitorResponse>();
        payload.Should().NotBeNull();
        payload!.LiveSessionId.Should().Be(seeded.LiveSessionId);
        payload.SubstageSnapshotId.Should().Be(seeded.SubstageSnapshotId);
        payload.QuestionSequenceOrder.Should().Be(1);

        payload.Teams.Should().HaveCount(2);
        // Locate each cell by the runtime TeamId we seeded, not by list position — the projection orders
        // teams by TeamCode, so a positional pick would resolve the wrong team.
        var answered = payload.Teams.Single(team => team.TeamId == seeded.AnsweredTeamId);
        answered.TeamCode.Should().Be("BLU-01");
        answered.Answered.Should().BeTrue();
        answered.AnsweredAt.Should().NotBeNull();

        var notAnswered = payload.Teams.Single(team => team.TeamId == seeded.NotAnsweredTeamId);
        notAnswered.TeamCode.Should().Be("RED-01");
        notAnswered.Answered.Should().BeFalse();
        notAnswered.AnsweredAt.Should().BeNull();
    }

    [Fact]
    public async Task GetAnsweredMonitor_AsNonOwningOperator_ReturnsForbiddenProblemDetails()
    {
        var seeded = await SeedTriviaSessionWithOneTeamAnsweredAsync();
        SetCurrentActor(OtherOperatorUserId, OtherOperatorExternalIdentityId);
        AddTrustedHeaders(_client, OtherOperatorExternalIdentityId, "Operator", "other-operator@example.com");

        var response = await _client.GetAsync(BuildMonitorUrl(seeded.LiveSessionId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Forbidden);
        problem.Type.Should().Be("forbidden-access");
    }

    [Fact]
    public async Task GetAnsweredMonitor_ResponseBody_NeverCarriesOptionCorrectnessOrPoints()
    {
        var seeded = await SeedTriviaSessionWithOneTeamAnsweredAsync();
        SetCurrentActor(OwnerOperatorUserId, OwnerOperatorExternalIdentityId);
        AddTrustedHeaders(_client, OwnerOperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await _client.GetAsync(BuildMonitorUrl(seeded.LiveSessionId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        // The chosen option was seeded (AnsweredOptionSequenceOrder), yet the pre-close monitor must not
        // reveal it, its correctness, or its score. Assert on the wire JSON so a leak in serialization
        // is caught even if the typed DTO omits the fields.
        raw.Should().NotContainAny(
            "selectedOptionSequenceOrder",
            "isCorrect",
            "scoreValue",
            "points",
            "correctOption");
    }

    private async Task<SeededMonitorSession> SeedTriviaSessionWithOneTeamAnsweredAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTimeOffset.UtcNow;
        var createdAt = now.AddMinutes(-20);
        var sourceMissionId = Guid.NewGuid();
        var snapshot = CreateTriviaSnapshot(sourceMissionId);
        var substageSnapshotId = snapshot.StageSnapshots.Single().SubstageSnapshots.Single().SubstageSnapshotId;

        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"TRV-{Guid.NewGuid():N}"[..12],
            "Operator Answered Monitor Trivia",
            maximumTimeMinutes: 45,
            createdAt,
            snapshot);

        // AssociateTeam mints a fresh runtime Team.TeamId (the passed guid becomes ReferenceTeamId), and
        // the projection returns that runtime id — so capture it here to locate cells by real identity.
        var answeredTeam = session.AssociateTeam(Guid.NewGuid(), "Blue", "BLU-01", 4);
        var notAnsweredTeam = session.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);
        session.AssignOperator(OwnerOperatorUserId, createdAt);

        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(1), transitionPolicy);
        session.MoveTo(SessionState.Active, createdAt.AddMinutes(2), transitionPolicy);
        session.ActivateQuestion(0, now);

        // One team answers the active question; the other stays not-answered (derived from absence).
        session.RegisterTriviaAnswer(answeredTeam.TeamId, AnsweredOptionSequenceOrder, Guid.NewGuid(), now.AddSeconds(2));

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededMonitorSession(
            session.LiveSessionId,
            substageSnapshotId,
            answeredTeam.TeamId,
            notAnsweredTeam.TeamId);
    }

    private static MissionRuntimeSnapshot CreateTriviaSnapshot(Guid sourceMissionId)
    {
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Operator Answered Monitor Quiz",
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

    private void SetCurrentActor(int userId, string externalIdentityId)
    {
        _factory.AuthenticatedActorProfileAccessClient.CurrentActor = new AuthenticatedActorProfileLookupDto(
            userId,
            externalIdentityId,
            "Operator",
            true);
    }

    private static string BuildMonitorUrl(Guid liveSessionId)
    {
        return $"/api/sessions/{liveSessionId:D}/answered-monitor";
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

    private sealed record SeededMonitorSession(
        Guid LiveSessionId,
        Guid SubstageSnapshotId,
        Guid AnsweredTeamId,
        Guid NotAnsweredTeamId);

    private sealed record TriviaAnsweredMonitorResponse(
        Guid LiveSessionId,
        Guid SubstageSnapshotId,
        int QuestionSequenceOrder,
        IReadOnlyList<TriviaTeamAnsweredStatusResponse> Teams);

    private sealed record TriviaTeamAnsweredStatusResponse(
        Guid TeamId,
        string TeamCode,
        string DisplayName,
        bool Answered,
        DateTimeOffset? AnsweredAt);

    private sealed record ProblemDetailsResponse(string? Type, string? Title, int? Status, string? Detail);
}
