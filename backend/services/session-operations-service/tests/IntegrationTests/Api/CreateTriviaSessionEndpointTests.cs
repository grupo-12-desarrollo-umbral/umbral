using Microsoft.AspNetCore.Mvc;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class CreateTriviaSessionEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public CreateTriviaSessionEndpointTests(PostgreSqlFixture fixture)
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
    public async Task CreateTriviaSession_WithPublishedQuiz_ReturnsCreatedAndPersistsFixedCopy()
    {
        const int sourceTriviaQuizId = 42;
        var scheduledAt = new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero);
        _factory.TriviaQuizSource.Quiz = CreateQuiz(sourceTriviaQuizId, "Published");
        AddTrustedHeaders(_client, "kc-admin-1", "Administrator", "admin@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/sessions",
            new
            {
                sourceTriviaQuizId,
                title = "Smoke Trivia",
                maximumTimeMinutes = 10,
                scheduledAt
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var payload = await response.Content.ReadFromJsonAsync<CreateTriviaSessionResponse>();
        payload.Should().NotBeNull();
        payload!.Title.Should().Be("Smoke Trivia");
        payload.SessionState.Should().Be(nameof(SessionState.Scheduled));
        payload.SourceTriviaQuizId.Should().Be(sourceTriviaQuizId);
        payload.QuestionCount.Should().Be(1);
        response.Headers.Location!.ToString().Should().EndWith($"/api/sessions/{payload.LiveSessionId:D}");

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedSession = await dbContext.LiveSessions
            .Include(session => session.TriviaSnapshot!)
                .ThenInclude(snapshot => snapshot.Questions)
                    .ThenInclude(question => question.Options)
            .SingleAsync(session => session.LiveSessionId == payload.LiveSessionId);

        persistedSession.Source.SourceTriviaQuizId.Should().Be(sourceTriviaQuizId);
        persistedSession.AssignedOperatorUserId.Should().BeNull();
        persistedSession.TriviaSnapshot.Should().NotBeNull();
        persistedSession.TriviaSnapshot!.QuizTitle.Should().Be("Quiz Night");
        persistedSession.TriviaSnapshot.Questions.Should().ContainSingle();
        persistedSession.TriviaSnapshot.Questions.Single().Options.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateTriviaSession_AsAdministrator_MakesUnassignedSessionVisibleInAssignmentList()
    {
        const int sourceTriviaQuizId = 42;
        var scheduledAt = new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero);
        _factory.TriviaQuizSource.Quiz = CreateQuiz(sourceTriviaQuizId, "Published");
        AddTrustedHeaders(_client, "kc-admin-1", "Administrator", "admin@example.com");

        var createResponse = await _client.PostAsJsonAsync(
            "/api/sessions",
            new
            {
                sourceTriviaQuizId,
                title = "Unassigned Smoke Trivia",
                maximumTimeMinutes = 10,
                scheduledAt
            });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdPayload = await createResponse.Content.ReadFromJsonAsync<CreateTriviaSessionResponse>();
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
    public async Task CreateTriviaSession_AsOperator_ReturnsForbidden()
    {
        _factory.TriviaQuizSource.Quiz = CreateQuiz(42, "Published");
        AddTrustedHeaders(_client, "kc-operator-27", "Operator", "operator@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/sessions",
            new
            {
                sourceTriviaQuizId = 42,
                title = "Smoke Trivia",
                maximumTimeMinutes = 10,
                scheduledAt = new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero)
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Archived")]
    public async Task CreateTriviaSession_WithNonPublishedQuiz_ReturnsConflict(string status)
    {
        const int sourceTriviaQuizId = 42;
        _factory.TriviaQuizSource.Quiz = CreateQuiz(sourceTriviaQuizId, status);
        AddTrustedHeaders(_client, "kc-admin-1", "Administrator", "admin@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/sessions",
            new
            {
                sourceTriviaQuizId,
                title = "Smoke Trivia",
                maximumTimeMinutes = 10,
                scheduledAt = new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero)
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Conflict.");
        problem.Detail.Should().Contain(status);
    }

    [Fact]
    public async Task CreateTriviaSession_WithoutTrustedHeaders_ReturnsUnauthorized()
    {
        _factory.TriviaQuizSource.Quiz = CreateQuiz(42, "Published");

        var response = await _client.PostAsJsonAsync(
            "/api/sessions",
            new
            {
                sourceTriviaQuizId = 42,
                title = "Smoke Trivia",
                maximumTimeMinutes = 10,
                scheduledAt = new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero)
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateTriviaSession_WithParticipantRole_ReturnsForbidden()
    {
        _factory.TriviaQuizSource.Quiz = CreateQuiz(42, "Published");
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/sessions",
            new
            {
                sourceTriviaQuizId = 42,
                title = "Smoke Trivia",
                maximumTimeMinutes = 10,
                scheduledAt = new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero)
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static PublishedTriviaQuizDto CreateQuiz(int triviaQuizId, string status)
    {
        return new PublishedTriviaQuizDto(
            triviaQuizId,
            "Quiz Night",
            status,
            [
                new PublishedTriviaQuestionDto(
                    7,
                    "Capital of France?",
                    2,
                    true,
                    50,
                    30,
                    "Paris is the capital city.",
                    [
                        new PublishedTriviaOptionDto(1, "Paris", 2, true),
                        new PublishedTriviaOptionDto(2, "Lyon", 1, false)
                    ]),
                new PublishedTriviaQuestionDto(
                    8,
                    "Ignored inactive question",
                    1,
                    false,
                    25,
                    20,
                    null,
                    [
                        new PublishedTriviaOptionDto(3, "A", 1, true),
                        new PublishedTriviaOptionDto(4, "B", 2, false)
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

    private sealed record CreateTriviaSessionResponse(
        Guid LiveSessionId,
        string SessionCode,
        string Title,
        string SessionState,
        DateTimeOffset ScheduledAt,
        int SourceTriviaQuizId,
        int QuestionCount);

    private sealed record SessionOperatorSummaryResponse(
        Guid LiveSessionId,
        string SessionCode,
        string Title,
        string SessionState,
        int? AssignedOperatorUserId,
        DateTimeOffset ScheduledAt,
        DateTimeOffset? LastTransitionedAt);
}
