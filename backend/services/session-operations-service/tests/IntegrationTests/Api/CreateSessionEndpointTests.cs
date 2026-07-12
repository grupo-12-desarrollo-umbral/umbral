using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using umbral_backend.Api.Controllers;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Enums;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class CreateSessionEndpointTests : IAsyncLifetime
{
    private const int MissionId = 7;

    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public CreateSessionEndpointTests(PostgreSqlFixture fixture)
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
    public async Task CreateSession_WithEligibleMission_ReturnsCreatedAndPersistsRuntimeSnapshot()
    {
        var scheduledAt = new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero);
        _factory.MissionRuntimeSource.Runtime = TriviaRuntime();
        _factory.MissionReadinessSource.Readiness = EligibleMission();
        AddTrustedHeaders(_client, "kc-admin-1", "Administrator", "admin@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/sessions",
            new
            {
                missionId = MissionId,
                title = "Smoke Trivia",
                maximumTimeMinutes = 10,
                scheduledAt
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var payload = await response.Content.ReadFromJsonAsync<CreateSessionResponse>();
        payload.Should().NotBeNull();
        payload!.Title.Should().Be("Smoke Trivia");
        payload.SessionState.Should().Be(nameof(SessionState.Scheduled));
        response.Headers.Location!.ToString().Should().EndWith($"/api/sessions/{payload.LiveSessionId:D}");

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedSession = await dbContext.LiveSessions
            .Include(session => session.MissionRuntimeSnapshot)
                .ThenInclude(snapshot => snapshot.TriviaQuestionSnapshots)
                    .ThenInclude(question => question.Options)
            .SingleAsync(session => session.LiveSessionId == payload.LiveSessionId);

        persistedSession.AssignedOperatorUserId.Should().BeNull();
        persistedSession.MissionRuntimeSnapshot.Should().NotBeNull();
        persistedSession.MissionRuntimeSnapshot.MissionTitle.Should().Be("Quiz Night");
        persistedSession.MissionRuntimeSnapshot.TriviaQuestionSnapshots.Should().ContainSingle();
        persistedSession.MissionRuntimeSnapshot.TriviaQuestionSnapshots.Single().Options.Should().HaveCount(2);
    }

    // HU-16: snapshot-content-fidelity at the endpoint — a trivia-bearing mission with a
    // multi-question quiz must freeze EVERY question/option (none dropped), in strict mission
    // order, with each correct flag/score/timer intact. A single-question quiz cannot prove this.
    [Fact]
    public async Task CreateSession_WithMultiQuestionTriviaMission_FreezesTheWholeQuizInOrder()
    {
        _factory.MissionRuntimeSource.Runtime = MultiQuestionTriviaRuntime();
        _factory.MissionReadinessSource.Readiness = EligibleMission();
        AddTrustedHeaders(_client, "kc-admin-1", "Administrator", "admin@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/sessions",
            new
            {
                missionId = MissionId,
                title = "Whole Quiz",
                maximumTimeMinutes = 10,
                scheduledAt = new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero)
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<CreateSessionResponse>();
        payload.Should().NotBeNull();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await dbContext.LiveSessions
            .Include(session => session.MissionRuntimeSnapshot)
                .ThenInclude(snapshot => snapshot.TriviaQuestionSnapshots)
                    .ThenInclude(question => question.Options)
            .SingleAsync(session => session.LiveSessionId == payload!.LiveSessionId);

        var questions = persisted.MissionRuntimeSnapshot.TriviaQuestionSnapshots
            .OrderBy(question => question.SequenceOrder)
            .ToArray();

        questions.Should().HaveCount(3);
        questions.Select(question => question.Prompt).Should().ContainInOrder("Q1", "Q2", "Q3");
        questions.Select(question => question.SequenceOrder).Should().ContainInOrder(1, 2, 3);
        questions.Select(question => question.ScoreValue).Should().ContainInOrder(10, 20, 30);
        questions.Select(question => question.TimeLimitSeconds).Should().ContainInOrder(15, 25, 35);

        foreach (var question in questions)
        {
            var options = question.Options.OrderBy(option => option.SequenceOrder).ToArray();
            options.Should().HaveCount(2);
            options.Count(option => option.IsCorrect).Should().Be(1);
            options.Single(option => option.IsCorrect).OptionText.Should().Be($"{question.Prompt}-correct");
        }
    }

    [Fact]
    public async Task CreateSession_AsAdministrator_MakesUnassignedSessionVisibleInAssignmentList()
    {
        var scheduledAt = new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero);
        _factory.MissionRuntimeSource.Runtime = TriviaRuntime();
        _factory.MissionReadinessSource.Readiness = EligibleMission();
        AddTrustedHeaders(_client, "kc-admin-1", "Administrator", "admin@example.com");

        var createResponse = await _client.PostAsJsonAsync(
            "/api/sessions",
            new
            {
                missionId = MissionId,
                title = "Unassigned Smoke Trivia",
                maximumTimeMinutes = 10,
                scheduledAt
            });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdPayload = await createResponse.Content.ReadFromJsonAsync<CreateSessionResponse>();
        createdPayload.Should().NotBeNull();

        var listResponse = await _client.GetAsync("/api/sessions");

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listedSessions = await listResponse.Content.ReadFromJsonAsync<List<SessionOperatorSummaryResponse>>();
        listedSessions.Should().NotBeNull();
        listedSessions!
            .Should()
            .ContainSingle(session =>
                session.LiveSessionId == createdPayload!.LiveSessionId &&
                session.AssignedOperatorUserId == null &&
                session.Title == "Unassigned Smoke Trivia");
    }

    [Fact]
    public async Task CreateSession_AsOperator_ReturnsForbidden()
    {
        _factory.MissionRuntimeSource.Runtime = TriviaRuntime();
        _factory.MissionReadinessSource.Readiness = EligibleMission();
        AddTrustedHeaders(_client, "kc-operator-27", "Operator", "operator@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/sessions",
            new
            {
                missionId = MissionId,
                title = "Smoke Trivia",
                maximumTimeMinutes = 10,
                scheduledAt = new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero)
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateSession_WithDeactivatedMission_ReturnsConflictAndPersistsNothing()
    {
        _factory.MissionRuntimeSource.Runtime = TriviaRuntime();
        _factory.MissionReadinessSource.Readiness =
            new MissionReadinessDto(MissionId, "Inactive", IsActive: false, IsReady: false, []);
        AddTrustedHeaders(_client, "kc-admin-1", "Administrator", "admin@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/sessions",
            new
            {
                missionId = MissionId,
                title = "Smoke Trivia",
                maximumTimeMinutes = 10,
                scheduledAt = new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero)
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Conflict.");

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await dbContext.LiveSessions.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task CreateSession_WithUnknownMission_ReturnsNotFound()
    {
        _factory.MissionRuntimeSource.Runtime = TriviaRuntime();
        _factory.MissionReadinessSource.Readiness = null;
        AddTrustedHeaders(_client, "kc-admin-1", "Administrator", "admin@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/sessions",
            new
            {
                missionId = MissionId,
                title = "Smoke Trivia",
                maximumTimeMinutes = 10,
                scheduledAt = new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero)
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateSession_WithEligibleMissionButMissingRuntimePlan_ReturnsNotFound()
    {
        _factory.MissionRuntimeSource.Runtime = null;
        _factory.MissionReadinessSource.Readiness = EligibleMission();
        AddTrustedHeaders(_client, "kc-admin-1", "Administrator", "admin@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/sessions",
            new
            {
                missionId = MissionId,
                title = "Smoke Trivia",
                maximumTimeMinutes = 10,
                scheduledAt = new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero)
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await dbContext.LiveSessions.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task CreateSession_WithoutTrustedHeaders_ReturnsUnauthorized()
    {
        _factory.MissionRuntimeSource.Runtime = TriviaRuntime();
        _factory.MissionReadinessSource.Readiness = EligibleMission();

        var response = await _client.PostAsJsonAsync(
            "/api/sessions",
            new
            {
                missionId = MissionId,
                title = "Smoke Trivia",
                maximumTimeMinutes = 10,
                scheduledAt = new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero)
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateSession_WithParticipantRole_ReturnsForbidden()
    {
        _factory.MissionRuntimeSource.Runtime = TriviaRuntime();
        _factory.MissionReadinessSource.Readiness = EligibleMission();
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/sessions",
            new
            {
                missionId = MissionId,
                title = "Smoke Trivia",
                maximumTimeMinutes = 10,
                scheduledAt = new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero)
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateSession_WhenTriviaSubstageResolvesEmpty_ReturnsConflictAndPersistsNothing()
    {
        _factory.MissionRuntimeSource.Runtime = RuntimeWithEmptyTriviaSubstage();
        _factory.MissionReadinessSource.Readiness = EligibleMission();
        AddTrustedHeaders(_client, "kc-admin-1", "Administrator", "admin@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/sessions",
            new
            {
                missionId = MissionId,
                title = "Smoke Trivia",
                maximumTimeMinutes = 10,
                scheduledAt = new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero)
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Conflict.");

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await dbContext.LiveSessions.AnyAsync()).Should().BeFalse();
    }

    // HU-17: lock the single-source invariant at the API contract — the create request
    // exposes Mission as the only source; no quiz/second-source/mode field is representable.
    [Fact]
    public void CreateSessionRequest_ExposesMissionAsTheOnlySource()
    {
        var propertyNames = typeof(SessionsController.CreateSessionRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        propertyNames.Should().BeEquivalentTo(
            new[] { "MissionId", "Title", "MaximumTimeMinutes", "ScheduledAt" });

        propertyNames.Should().NotContain(name =>
            name.Contains("Quiz", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Trivia", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Source", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Mode", StringComparison.OrdinalIgnoreCase));
    }

    // HU-17: there is exactly one session-creation entry point (POST /api/sessions) and
    // no alternate/quiz-as-source creation route survives the realignment.
    [Fact]
    public void Sessions_ExposeNoAlternateCreationRoute()
    {
        var routeEndpoints = _factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

        routeEndpoints.Should().NotContain(endpoint =>
            (endpoint.RoutePattern.RawText ?? string.Empty)
                .Contains("quiz", StringComparison.OrdinalIgnoreCase) ||
            (endpoint.RoutePattern.RawText ?? string.Empty)
                .Contains("trivia", StringComparison.OrdinalIgnoreCase));

        var creationRoutes = routeEndpoints
            .Where(endpoint =>
                string.Equals(endpoint.RoutePattern.RawText, "api/sessions", StringComparison.OrdinalIgnoreCase) &&
                (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("POST") ?? false))
            .ToArray();

        creationRoutes.Should().ContainSingle();
    }

    private static MissionReadinessDto EligibleMission()
    {
        return new MissionReadinessDto(MissionId, "Ready", IsActive: true, IsReady: true, []);
    }

    private static MissionRuntimeDto RuntimeWithEmptyTriviaSubstage()
    {
        return new MissionRuntimeDto(
            "Quiz Night",
            10,
            [
                new MissionRuntimeStageDto(
                    "Stage One",
                    1,
                    [
                        new MissionRuntimeSubstageDto(
                            "Trivia Round",
                            1,
                            "Trivia",
                            [],
                            [],
                            [])
                    ])
            ]);
    }

    private static MissionRuntimeDto TriviaRuntime()
    {
        return new MissionRuntimeDto(
            "Quiz Night",
            10,
            [
                new MissionRuntimeStageDto(
                    "Stage One",
                    1,
                    [
                        new MissionRuntimeSubstageDto(
                            "Trivia Round",
                            1,
                            "Trivia",
                            [],
                            [
                                new MissionRuntimeTriviaQuestionDto(
                                    "Capital of France?",
                                    1,
                                    50,
                                    30,
                                    "Paris is the capital city.",
                                    [
                                        new MissionRuntimeTriviaOptionDto("Paris", 1, true),
                                        new MissionRuntimeTriviaOptionDto("Lyon", 2, false)
                                    ])
                            ],
                            [])
                    ])
            ]);
    }

    private static MissionRuntimeDto MultiQuestionTriviaRuntime()
    {
        return new MissionRuntimeDto(
            "Quiz Night",
            10,
            [
                new MissionRuntimeStageDto(
                    "Stage One",
                    1,
                    [
                        new MissionRuntimeSubstageDto(
                            "Trivia Round",
                            1,
                            "Trivia",
                            [],
                            [
                                new MissionRuntimeTriviaQuestionDto(
                                    "Q1", 1, 10, 15, null,
                                    [
                                        new MissionRuntimeTriviaOptionDto("Q1-correct", 1, true),
                                        new MissionRuntimeTriviaOptionDto("Q1-wrong", 2, false)
                                    ]),
                                new MissionRuntimeTriviaQuestionDto(
                                    "Q2", 2, 20, 25, null,
                                    [
                                        new MissionRuntimeTriviaOptionDto("Q2-wrong", 1, false),
                                        new MissionRuntimeTriviaOptionDto("Q2-correct", 2, true)
                                    ]),
                                new MissionRuntimeTriviaQuestionDto(
                                    "Q3", 3, 30, 35, null,
                                    [
                                        new MissionRuntimeTriviaOptionDto("Q3-correct", 1, true),
                                        new MissionRuntimeTriviaOptionDto("Q3-wrong", 2, false)
                                    ])
                            ],
                            [])
                    ])
            ]);
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

    private sealed record CreateSessionResponse(
        Guid LiveSessionId,
        string SessionCode,
        string Title,
        string SessionState,
        DateTimeOffset ScheduledAt);

    private sealed record SessionOperatorSummaryResponse(
        Guid LiveSessionId,
        string SessionCode,
        string Title,
        string SessionState,
        int? AssignedOperatorUserId,
        DateTimeOffset ScheduledAt,
        DateTimeOffset? LastTransitionedAt);
}
