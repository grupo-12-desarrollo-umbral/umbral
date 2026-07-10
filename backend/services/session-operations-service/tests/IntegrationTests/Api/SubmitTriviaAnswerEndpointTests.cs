using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

// HU-34 participant answer write: the first in-time answer to the active trivia question succeeds with
// acceptance metadata only; repeat/late/no-active-question attempts return RFC 7807 ProblemDetails; a
// denied participation fact is Forbidden. The 200 body never carries correctness or points.
[Collection(PostgreSqlCollection.Name)]
public sealed class SubmitTriviaAnswerEndpointTests : IAsyncLifetime
{
    private const int QuestionTimeLimitSeconds = 300;

    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public SubmitTriviaAnswerEndpointTests(PostgreSqlFixture fixture)
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
    public async Task SubmitAnswer_FirstInTimeAnswer_ReturnsAcceptanceMetadataWithoutCorrectnessOrPoints()
    {
        var seeded = await SeedTriviaSessionAsync(TriviaAnswerSeedState.ActiveQuestion);
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(BuildAnswersUrl(seeded), CorrectAnswer(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<SubmitTriviaAnswerResponse>();
        payload.Should().NotBeNull();
        payload!.LiveSessionId.Should().Be(seeded.LiveSessionId);
        payload.TeamId.Should().Be(seeded.TeamId);
        payload.TriviaSubstageSnapshotId.Should().Be(seeded.TriviaSubstageSnapshotId);
        payload.QuestionSequenceOrder.Should().Be(1);
        payload.AnsweredAt.Should().NotBe(default);

        // Privacy gate: the accepted-answer response must never expose correctness, points, or which
        // option was picked — those stay internal / RabbitMQ-only until close (HU-35) / scoring (HU-37).
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var propertyNames = document.RootElement.EnumerateObject()
            .Select(property => property.Name.ToLowerInvariant())
            .ToList();
        propertyNames.Should().NotContain(name =>
            name.Contains("correct") || name.Contains("score") || name.Contains("option"));
    }

    [Fact]
    public async Task SubmitAnswer_RepeatAttemptForSameTeamAndQuestion_ReturnsConflictProblemDetails()
    {
        var seeded = await SeedTriviaSessionAsync(TriviaAnswerSeedState.ActiveQuestion);
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var first = await _client.PostAsJsonAsync(BuildAnswersUrl(seeded), CorrectAnswer(seeded));
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // A second answer for the same team/question loses to the first-write-wins rule.
        var repeat = await _client.PostAsJsonAsync(BuildAnswersUrl(seeded), OtherAnswer(seeded));

        repeat.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await repeat.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Conflict.");
        ReasonCode(problem).Should().Be("duplicate-trivia-answer");
    }

    [Fact]
    public async Task SubmitAnswer_AfterWindowClosed_ReturnsConflictProblemDetails()
    {
        var seeded = await SeedTriviaSessionAsync(TriviaAnswerSeedState.ExpiredQuestion);
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(BuildAnswersUrl(seeded), CorrectAnswer(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        ReasonCode(problem).Should().Be("late-trivia-answer");
    }

    [Fact]
    public async Task SubmitAnswer_WhenNoActiveQuestion_ReturnsConflictProblemDetails()
    {
        var seeded = await SeedTriviaSessionAsync(TriviaAnswerSeedState.NoActiveQuestion);
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(BuildAnswersUrl(seeded), CorrectAnswer(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        // No activated question -> no active substage -> rejected as requiring a trivia substage.
        ReasonCode(problem).Should().Be("trivia-answer-requires-trivia-substage");
    }

    [Fact]
    public async Task SubmitAnswer_WhenParticipationFactDenied_ReturnsForbidden()
    {
        var seeded = await SeedTriviaSessionAsync(TriviaAnswerSeedState.ActiveQuestion);
        _factory.AccessClient.IsAllowed = false;
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(BuildAnswersUrl(seeded), CorrectAnswer(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("Forbidden.");
    }

    [Fact]
    public async Task SubmitAnswer_WithoutTrustedHeaders_ReturnsUnauthorized()
    {
        var seeded = await SeedTriviaSessionAsync(TriviaAnswerSeedState.ActiveQuestion);

        var response = await _client.PostAsJsonAsync(BuildAnswersUrl(seeded), CorrectAnswer(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SubmitAnswer_WithOperatorRole_ReturnsForbidden()
    {
        var seeded = await SeedTriviaSessionAsync(TriviaAnswerSeedState.ActiveQuestion);
        AddTrustedHeaders(_client, "kc-operator-1", "Operator", "operator@example.com");

        var response = await _client.PostAsJsonAsync(BuildAnswersUrl(seeded), CorrectAnswer(seeded));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SubmitAnswer_WhenSessionDoesNotExist_ReturnsNotFound()
    {
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{Guid.NewGuid():D}/participants/answers",
            new SubmitTriviaAnswerRequest(Guid.NewGuid(), Guid.NewGuid(), 1, 1, null));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubmitAnswer_WithNonPositiveOptionOrder_ReturnsBadRequest()
    {
        var seeded = await SeedTriviaSessionAsync(TriviaAnswerSeedState.ActiveQuestion);
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(
            BuildAnswersUrl(seeded),
            new SubmitTriviaAnswerRequest(seeded.TeamId, seeded.TriviaSubstageSnapshotId, 1, 0, null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Validation failed.");
    }

    private static SubmitTriviaAnswerRequest CorrectAnswer(SeededSession seeded) =>
        new(seeded.TeamId, seeded.TriviaSubstageSnapshotId, 1, 1, null);

    private static SubmitTriviaAnswerRequest OtherAnswer(SeededSession seeded) =>
        new(seeded.TeamId, seeded.TriviaSubstageSnapshotId, 1, 2, null);

    private static string ReasonCode(ProblemDetails problem) => problem.Type ?? string.Empty;

    private async Task<SeededSession> SeedTriviaSessionAsync(TriviaAnswerSeedState seedState)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTimeOffset.UtcNow;
        var createdAt = now.AddMinutes(-20);
        var sourceMissionId = Guid.NewGuid();
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);

        // The no-active-question case seeds a treasure-hunt mission: the active substage is not a
        // trivia substage, so there is no answerable question even in the Active state (the round-start
        // handler auto-activates a first trivia question on save, so an Active trivia session always
        // has one). Every other case uses the trivia snapshot.
        var snapshot = seedState == TriviaAnswerSeedState.NoActiveQuestion
            ? CreateTreasureHuntSnapshot(sourceMissionId)
            : CreateTriviaSnapshot(sourceMissionId, triviaSubstage);

        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"TRV-{Guid.NewGuid():N}"[..12],
            "Trivia Answer Session",
            maximumTimeMinutes: 45,
            createdAt,
            snapshot);
        var team = session.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);

        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(1), transitionPolicy);
        session.MoveTo(SessionState.Active, createdAt.AddMinutes(2), transitionPolicy);

        switch (seedState)
        {
            case TriviaAnswerSeedState.ActiveQuestion:
                session.ActivateQuestion(0, now);
                break;
            case TriviaAnswerSeedState.ExpiredQuestion:
                // Activate the window in the past so the wall-clock remaining is already negative.
                session.ActivateQuestion(0, createdAt.AddMinutes(3));
                break;
            case TriviaAnswerSeedState.NoActiveQuestion:
                // Treasure-hunt active substage — nothing to activate.
                break;
        }

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(session.LiveSessionId, team.TeamId, triviaSubstage.SubstageSnapshotId);
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(Guid sourceMissionId)
    {
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1, 100);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Treasure Mission",
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

    private static MissionRuntimeSnapshot CreateTriviaSnapshot(Guid sourceMissionId, SubstageSnapshot triviaSubstage)
    {
        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Answer Quiz",
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

    private static string BuildAnswersUrl(SeededSession seeded) =>
        $"/api/sessions/{seeded.LiveSessionId:D}/participants/answers";

    private static void AddTrustedHeaders(HttpClient client, string userId, string role, string email)
    {
        client.DefaultRequestHeaders.Remove("X-User-Id");
        client.DefaultRequestHeaders.Remove("X-User-Role");
        client.DefaultRequestHeaders.Remove("X-User-Email");
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-User-Role", role);
        client.DefaultRequestHeaders.Add("X-User-Email", email);
    }

    private enum TriviaAnswerSeedState
    {
        ActiveQuestion,
        ExpiredQuestion,
        NoActiveQuestion
    }

    private sealed record SeededSession(Guid LiveSessionId, Guid TeamId, Guid TriviaSubstageSnapshotId);

    private sealed record SubmitTriviaAnswerRequest(
        Guid TeamId,
        Guid TriviaSubstageSnapshotId,
        int QuestionSequenceOrder,
        int SelectedOptionSequenceOrder,
        string? Token);

    private sealed record SubmitTriviaAnswerResponse(
        Guid LiveSessionId,
        Guid TeamId,
        Guid TriviaSubstageSnapshotId,
        int QuestionSequenceOrder,
        DateTimeOffset AnsweredAt);
}
