using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Web.Endpoints;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

public sealed class TriviaEndpointsTests : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private MissionDesignApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public TriviaEndpointsTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new MissionDesignApiWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task CreateTriviaQuiz_ReturnsCreatedQuizWithAssociatedQuestionShape()
    {
        AddAdministratorHeaders();

        var response = await _client.PostAsJsonAsync(
            "/api/trivias/",
            new
            {
                title = "Capital Cities",
                description = "Identify the right capital.",
                questions = new[]
                {
                    new
                    {
                        prompt = "Capital of France?",
                        sequenceOrder = 1,
                        isActive = true,
                        options = new[]
                        {
                            new { optionText = "Paris", sequenceOrder = 1, isCorrect = true },
                            new { optionText = "Berlin", sequenceOrder = 2, isCorrect = false }
                        }
                    }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var payload = await response.Content.ReadFromJsonAsync<TriviasEndpoints.TriviaQuizResponse>();
        payload.Should().NotBeNull();
        payload!.Title.Should().Be("Capital Cities");
        payload.Description.Should().Be("Identify the right capital.");
        payload.Status.Should().Be("Draft");
        payload.Questions.Should().ContainSingle();
        payload.Questions[0].Prompt.Should().Be("Capital of France?");
        payload.Questions[0].Options.Select(option => option.OptionText).Should().Equal("Paris", "Berlin");
    }

    [Fact]
    public async Task GetTriviaCatalogAndDetail_ReturnPersistedDraftChanges()
    {
        AddAdministratorHeaders();

        var triviaId = await CreateTriviaQuizAsync("Historic Capitals");

        var catalogResponse = await _client.GetAsync("/api/trivias/");
        catalogResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var catalog = await catalogResponse.Content.ReadFromJsonAsync<IReadOnlyList<TriviasEndpoints.TriviaQuizSummaryResponse>>();
        catalog.Should().NotBeNull();
        catalog!.Should().ContainSingle();
        catalog[0].Id.Should().Be(triviaId);
        catalog[0].Title.Should().Be("Historic Capitals");
        catalog[0].Status.Should().Be("Draft");

        var detailResponse = await _client.GetAsync($"/api/trivias/{triviaId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await detailResponse.Content.ReadFromJsonAsync<TriviasEndpoints.TriviaQuizResponse>();
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(triviaId);
        detail.Questions.Should().ContainSingle();
        detail.Questions[0].Options.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateTriviaQuiz_ReturnsUpdatedQuiz()
    {
        AddAdministratorHeaders();

        var triviaId = await CreateTriviaQuizAsync("Trivia Before");

        var response = await _client.PutAsJsonAsync(
            $"/api/trivias/{triviaId}",
            new
            {
                title = "Trivia After",
                description = "Updated draft.",
                questions = new[]
                {
                    new
                    {
                        prompt = "2 + 2?",
                        sequenceOrder = 1,
                        isActive = true,
                        options = new[]
                        {
                            new { optionText = "4", sequenceOrder = 1, isCorrect = true },
                            new { optionText = "5", sequenceOrder = 2, isCorrect = false }
                        }
                    },
                    new
                    {
                        prompt = "3 + 3?",
                        sequenceOrder = 2,
                        isActive = true,
                        options = new[]
                        {
                            new { optionText = "6", sequenceOrder = 1, isCorrect = true },
                            new { optionText = "7", sequenceOrder = 2, isCorrect = false }
                        }
                    }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<TriviasEndpoints.TriviaQuizResponse>();
        payload.Should().NotBeNull();
        payload!.Id.Should().Be(triviaId);
        payload.Title.Should().Be("Trivia After");
        payload.Description.Should().Be("Updated draft.");
        payload.Questions.Select(question => question.SequenceOrder).Should().Equal(1, 2);
    }

    [Fact]
    public async Task CreateTriviaQuiz_WithNonAdminCaller_Returns403()
    {
        AddOperatorHeaders();

        var response = await _client.PostAsJsonAsync(
            "/api/trivias/",
            new
            {
                title = "Unauthorized Trivia",
                description = "Should be rejected.",
                questions = new[]
                {
                    new
                    {
                        prompt = "Capital of Spain?",
                        sequenceOrder = 1,
                        isActive = true,
                        options = new[]
                        {
                            new { optionText = "Madrid", sequenceOrder = 1, isCorrect = true },
                            new { optionText = "Lisbon", sequenceOrder = 2, isCorrect = false }
                        }
                    }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("Forbidden.");
    }

    [Fact]
    public async Task UpdateTriviaQuiz_WhenQuizIsPublished_ReturnsConflict()
    {
        AddAdministratorHeaders();

        var triviaId = await CreateTriviaQuizAsync("Published Trivia");
        await MarkTriviaQuizAsPublishedAsync(triviaId);

        var response = await _client.PutAsJsonAsync(
            $"/api/trivias/{triviaId}",
            new
            {
                title = "Published Trivia Updated",
                description = "Should be blocked.",
                questions = new[]
                {
                    new
                    {
                        prompt = "Still editable?",
                        sequenceOrder = 1,
                        isActive = true,
                        options = new[]
                        {
                            new { optionText = "No", sequenceOrder = 1, isCorrect = true },
                            new { optionText = "Yes", sequenceOrder = 2, isCorrect = false }
                        }
                    }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Trivia quiz cannot be edited in its current state.");
        problem.Detail.Should().Contain("cannot be edited");
    }

    private void AddAdministratorHeaders()
    {
        _client.DefaultRequestHeaders.Remove("X-User-Id");
        _client.DefaultRequestHeaders.Remove("X-User-Role");
        _client.DefaultRequestHeaders.Add("X-User-Id", "admin-01");
        _client.DefaultRequestHeaders.Add("X-User-Role", "Administrator");
    }

    private void AddOperatorHeaders()
    {
        _client.DefaultRequestHeaders.Remove("X-User-Id");
        _client.DefaultRequestHeaders.Remove("X-User-Role");
        _client.DefaultRequestHeaders.Add("X-User-Id", "operator-01");
        _client.DefaultRequestHeaders.Add("X-User-Role", "Operator");
    }

    private async Task<int> CreateTriviaQuizAsync(string title)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/trivias/",
            new
            {
                title,
                description = "Trivia briefing.",
                questions = new[]
                {
                    new
                    {
                        prompt = "Capital of Venezuela?",
                        sequenceOrder = 1,
                        isActive = true,
                        options = new[]
                        {
                            new { optionText = "Caracas", sequenceOrder = 1, isCorrect = true },
                            new { optionText = "Valencia", sequenceOrder = 2, isCorrect = false }
                        }
                    }
                }
            });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TriviasEndpoints.TriviaQuizResponse>();
        payload.Should().NotBeNull();

        return payload!.Id;
    }

    private async Task MarkTriviaQuizAsPublishedAsync(int triviaId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var triviaQuiz = await dbContext.TriviaQuizzes.SingleAsync(quiz => quiz.Id == triviaId);

        triviaQuiz.MarkAsPublished();
        await dbContext.SaveChangesAsync();
    }
}
