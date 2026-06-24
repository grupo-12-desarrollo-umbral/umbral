using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Web.Controllers;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

[Collection("MissionDesignIntegrationTests")]
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
                        scoreValue = 100,
                        timeLimitSeconds = 45,
                        explanation = "Paris is the French capital.",
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

        var payload = await response.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        payload.Should().NotBeNull();
        payload!.Title.Should().Be("Capital Cities");
        payload.Description.Should().Be("Identify the right capital.");
        payload.Status.Should().Be("Draft");
        payload.IsSourceReady.Should().BeFalse();
        payload.Questions.Should().ContainSingle();
        payload.Questions[0].Prompt.Should().Be("Capital of France?");
        payload.Questions[0].ScoreValue.Should().Be(100);
        payload.Questions[0].TimeLimitSeconds.Should().Be(45);
        payload.Questions[0].Explanation.Should().Be("Paris is the French capital.");
        payload.Questions[0].Options.Select(option => option.OptionText).Should().Equal("Paris", "Berlin");
    }

    [Fact]
    public async Task GetTriviaCatalogAndDetail_ReturnPersistedDraftChanges()
    {
        AddAdministratorHeaders();

        var triviaId = await CreateTriviaQuizAsync("Historic Capitals");

        var catalogResponse = await _client.GetAsync("/api/trivias/");
        catalogResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var catalog = await catalogResponse.Content.ReadFromJsonAsync<IReadOnlyList<TriviasController.TriviaQuizSummaryResponse>>();
        catalog.Should().NotBeNull();
        catalog!.Should().ContainSingle();
        catalog[0].Id.Should().Be(triviaId);
        catalog[0].Title.Should().Be("Historic Capitals");
        catalog[0].Status.Should().Be("Draft");
        catalog[0].IsSourceReady.Should().BeFalse();

        var detailResponse = await _client.GetAsync($"/api/trivias/{triviaId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await detailResponse.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(triviaId);
        detail.IsSourceReady.Should().BeFalse();
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
                        scoreValue = 25,
                        timeLimitSeconds = 30,
                        explanation = "Arithmetic baseline.",
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
                        scoreValue = 30,
                        timeLimitSeconds = 35,
                        explanation = "Second arithmetic baseline.",
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

        var payload = await response.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        payload.Should().NotBeNull();
        payload!.Id.Should().Be(triviaId);
        payload.Title.Should().Be("Trivia After");
        payload.Description.Should().Be("Updated draft.");
        payload.IsSourceReady.Should().BeFalse();
        payload.Questions.Select(question => question.SequenceOrder).Should().Equal(1, 2);
        payload.Questions[0].ScoreValue.Should().Be(25);
        payload.Questions[0].TimeLimitSeconds.Should().Be(30);
        payload.Questions[0].Explanation.Should().Be("Arithmetic baseline.");
    }

    [Fact]
    public async Task AddTriviaQuestion_ReturnsUpdatedQuizAndPersistsQuestionDetail()
    {
        AddAdministratorHeaders();

        var triviaId = await CreateTriviaQuizAsync("Question Authoring");

        var response = await _client.PostAsJsonAsync(
            $"/api/trivias/{triviaId}/questions",
            new
            {
                prompt = "Largest ocean?",
                sequenceOrder = 2,
                scoreValue = 100,
                timeLimitSeconds = 60,
                explanation = "The Pacific Ocean is the largest.",
                isActive = true,
                options = new[]
                {
                    new { optionText = "Pacific", sequenceOrder = 1, isCorrect = true },
                    new { optionText = "Atlantic", sequenceOrder = 2, isCorrect = false },
                    new { optionText = "Indian", sequenceOrder = 3, isCorrect = false }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        payload.Should().NotBeNull();
        payload!.Questions.Should().HaveCount(2);
        payload.IsSourceReady.Should().BeFalse();

        var addedQuestion = payload.Questions.Single(question => question.SequenceOrder == 2);
        addedQuestion.Prompt.Should().Be("Largest ocean?");
        addedQuestion.ScoreValue.Should().Be(100);
        addedQuestion.TimeLimitSeconds.Should().Be(60);
        addedQuestion.Explanation.Should().Be("The Pacific Ocean is the largest.");
        addedQuestion.Options.Should().HaveCount(3);
        addedQuestion.Options.Should().ContainSingle(option => option.IsCorrect && option.OptionText == "Pacific");

        var detailResponse = await _client.GetAsync($"/api/trivias/{triviaId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await detailResponse.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        detail.Should().NotBeNull();
        detail!.Questions.Should().ContainSingle(question =>
            question.SequenceOrder == 2 &&
            question.ScoreValue == 100 &&
            question.TimeLimitSeconds == 60 &&
            question.Explanation == "The Pacific Ocean is the largest.");
    }

    [Fact]
    public async Task UpdateTriviaQuestion_ReturnsUpdatedQuizAndDetail()
    {
        AddAdministratorHeaders();

        var triviaId = await CreateTriviaQuizAsync("Question Update");
        var questionId = await GetFirstQuestionIdAsync(triviaId);

        var response = await _client.PutAsJsonAsync(
            $"/api/trivias/{triviaId}/questions/{questionId}",
            new
            {
                prompt = "Capital of Colombia?",
                sequenceOrder = 1,
                scoreValue = 100,
                timeLimitSeconds = 50,
                explanation = "Bogota is the capital city.",
                isActive = true,
                options = new[]
                {
                    new { optionText = "Bogota", sequenceOrder = 1, isCorrect = true },
                    new { optionText = "Medellin", sequenceOrder = 2, isCorrect = false },
                    new { optionText = "Cali", sequenceOrder = 3, isCorrect = false }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        payload.Should().NotBeNull();
        payload!.IsSourceReady.Should().BeFalse();

        var updatedQuestion = payload.Questions.Single(question => question.Id == questionId);
        updatedQuestion.Prompt.Should().Be("Capital of Colombia?");
        updatedQuestion.ScoreValue.Should().Be(100);
        updatedQuestion.TimeLimitSeconds.Should().Be(50);
        updatedQuestion.Explanation.Should().Be("Bogota is the capital city.");
        updatedQuestion.Options.Should().HaveCount(3);
        updatedQuestion.Options.Should().ContainSingle(option => option.IsCorrect && option.OptionText == "Bogota");

        var detailResponse = await _client.GetAsync($"/api/trivias/{triviaId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await detailResponse.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        detail.Should().NotBeNull();
        detail!.Questions.Should().ContainSingle(question =>
            question.Id == questionId &&
            question.Prompt == "Capital of Colombia?" &&
            question.ScoreValue == 100 &&
            question.TimeLimitSeconds == 50 &&
            question.Explanation == "Bogota is the capital city." &&
            question.Options.Count == 3);
    }

    [Fact]
    public async Task AddTriviaQuestion_WithNonAdminCaller_Returns403()
    {
        AddAdministratorHeaders();

        var triviaId = await CreateTriviaQuizAsync("Unauthorized Question Mutation");

        AddOperatorHeaders();

        var response = await _client.PostAsJsonAsync(
            $"/api/trivias/{triviaId}/questions",
            new
            {
                prompt = "Capital of Spain?",
                sequenceOrder = 2,
                scoreValue = 75,
                timeLimitSeconds = 25,
                explanation = "Madrid is the capital.",
                isActive = true,
                options = new[]
                {
                    new
                    {
                        optionText = "Madrid",
                        sequenceOrder = 1,
                        isCorrect = true
                    },
                    new
                    {
                        optionText = "Lisbon",
                        sequenceOrder = 2,
                        isCorrect = false
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
        problem.Title.Should().Be("Conflict.");
        problem.Detail.Should().Contain("cannot be edited");
    }

    [Fact]
    public async Task PublishTriviaQuiz_ReturnsPublishedQuizAndSourceReadyProjection()
    {
        AddAdministratorHeaders();

        var triviaId = await CreateTriviaQuizAsync("Publication Candidate");

        var response = await _client.PostAsync($"/api/trivias/{triviaId}/publish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        payload.Should().NotBeNull();
        payload!.Id.Should().Be(triviaId);
        payload.Status.Should().Be("Published");
        payload.IsSourceReady.Should().BeTrue();

        var catalogResponse = await _client.GetAsync("/api/trivias/");
        catalogResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var catalog = await catalogResponse.Content.ReadFromJsonAsync<IReadOnlyList<TriviasController.TriviaQuizSummaryResponse>>();
        catalog.Should().NotBeNull();
        catalog!.Single().Status.Should().Be("Published");
        catalog.Single().IsSourceReady.Should().BeTrue();

        var detailResponse = await _client.GetAsync($"/api/trivias/{triviaId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await detailResponse.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        detail.Should().NotBeNull();
        detail!.Status.Should().Be("Published");
        detail.IsSourceReady.Should().BeTrue();
    }

    [Fact]
    public async Task PublishTriviaQuiz_WhenQuizFailsReadinessRules_ReturnsConflict()
    {
        AddAdministratorHeaders();

        var triviaId = await CreateEmptyTriviaQuizAsync("Incomplete Quiz");

        var response = await _client.PostAsync($"/api/trivias/{triviaId}/publish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Conflict.");
        problem.Detail.Should().Contain("at least one question");

        var detailResponse = await _client.GetAsync($"/api/trivias/{triviaId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await detailResponse.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        detail.Should().NotBeNull();
        detail!.Status.Should().Be("Draft");
        detail.IsSourceReady.Should().BeFalse();
    }

    [Fact]
    public async Task ArchiveTriviaQuiz_ReturnsArchivedQuizAndRemovesSourceReadiness()
    {
        AddAdministratorHeaders();

        var triviaId = await CreateTriviaQuizAsync("Archivable Quiz");
        await PublishTriviaQuizAsync(triviaId);

        var response = await _client.PostAsync($"/api/trivias/{triviaId}/archive", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        payload.Should().NotBeNull();
        payload!.Status.Should().Be("Archived");
        payload.IsSourceReady.Should().BeFalse();

        var catalogResponse = await _client.GetAsync("/api/trivias/");
        catalogResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var catalog = await catalogResponse.Content.ReadFromJsonAsync<IReadOnlyList<TriviasController.TriviaQuizSummaryResponse>>();
        catalog.Should().NotBeNull();
        catalog!.Single().Status.Should().Be("Archived");
        catalog.Single().IsSourceReady.Should().BeFalse();

        var detailResponse = await _client.GetAsync($"/api/trivias/{triviaId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await detailResponse.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        detail.Should().NotBeNull();
        detail!.Status.Should().Be("Archived");
        detail.IsSourceReady.Should().BeFalse();
    }

    [Fact]
    public async Task DuplicateTriviaQuiz_ReturnsCreatedAuthoringCopyWithLineageProjection()
    {
        AddAdministratorHeaders();

        var sourceTriviaId = await CreateTriviaQuizAsync("Source Trivia");
        await PublishTriviaQuizAsync(sourceTriviaId);

        var response = await _client.PostAsync($"/api/trivias/{sourceTriviaId}/duplicate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var payload = await response.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        payload.Should().NotBeNull();
        payload!.Id.Should().NotBe(sourceTriviaId);
        payload.Status.Should().Be("Draft");
        payload.IsSourceReady.Should().BeFalse();
        payload.SourceTriviaQuizId.Should().Be(sourceTriviaId);
        payload.HasUsageHistory.Should().BeFalse();
        payload.IsDuplicate.Should().BeTrue();
        payload.Questions.Should().ContainSingle();

        var sourceDetailResponse = await _client.GetAsync($"/api/trivias/{sourceTriviaId}");
        sourceDetailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var sourceDetail = await sourceDetailResponse.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        sourceDetail.Should().NotBeNull();
        sourceDetail!.Id.Should().Be(sourceTriviaId);
        sourceDetail.Status.Should().Be("Published");
        sourceDetail.SourceTriviaQuizId.Should().BeNull();
        sourceDetail.HasUsageHistory.Should().BeFalse();
        sourceDetail.IsDuplicate.Should().BeFalse();

        var duplicateDetailResponse = await _client.GetAsync($"/api/trivias/{payload.Id}");
        duplicateDetailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var duplicateDetail = await duplicateDetailResponse.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        duplicateDetail.Should().NotBeNull();
        duplicateDetail!.SourceTriviaQuizId.Should().Be(sourceTriviaId);
        duplicateDetail.IsDuplicate.Should().BeTrue();

        var catalogResponse = await _client.GetAsync("/api/trivias/");
        catalogResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var catalog = await catalogResponse.Content.ReadFromJsonAsync<IReadOnlyList<TriviasController.TriviaQuizSummaryResponse>>();
        catalog.Should().NotBeNull();
        catalog!.Should().Contain(item =>
            item.Id == sourceTriviaId &&
            item.Status == "Published" &&
            item.SourceTriviaQuizId == null &&
            !item.IsDuplicate);
        catalog.Should().Contain(item =>
            item.Id == payload.Id &&
            item.Status == "Draft" &&
            item.SourceTriviaQuizId == sourceTriviaId &&
            item.IsDuplicate);
    }

    [Fact]
    public async Task DeleteTriviaQuiz_WhenUnused_ReturnsNoContentAndRemovesQuiz()
    {
        AddAdministratorHeaders();

        var triviaId = await CreateTriviaQuizAsync("Disposable Trivia");

        var response = await _client.DeleteAsync($"/api/trivias/{triviaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detailResponse = await _client.GetAsync($"/api/trivias/{triviaId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTriviaQuiz_WhenUsed_ReturnsConflictAndKeepsHistoricalRecord()
    {
        AddAdministratorHeaders();

        var triviaId = await CreateTriviaQuizAsync("Used Trivia");
        await MarkTriviaQuizAsUsedAsync(triviaId);

        var response = await _client.DeleteAsync($"/api/trivias/{triviaId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Conflict.");
        problem.Detail.Should().Contain("cannot be removed destructively");

        var detailResponse = await _client.GetAsync($"/api/trivias/{triviaId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await detailResponse.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        detail.Should().NotBeNull();
        detail!.HasUsageHistory.Should().BeTrue();
    }

    [Fact]
    public async Task RetireTriviaQuiz_WhenUsed_ReturnsArchivedQuizAndPreservesHistoricalIdentity()
    {
        AddAdministratorHeaders();

        var triviaId = await CreateTriviaQuizAsync("Retirable Trivia");
        await PublishTriviaQuizAsync(triviaId);
        await MarkTriviaQuizAsUsedAsync(triviaId);

        var response = await _client.PostAsync($"/api/trivias/{triviaId}/retire", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        payload.Should().NotBeNull();
        payload!.Id.Should().Be(triviaId);
        payload.Status.Should().Be("Archived");
        payload.IsSourceReady.Should().BeFalse();
        payload.SourceTriviaQuizId.Should().BeNull();
        payload.HasUsageHistory.Should().BeTrue();
        payload.IsDuplicate.Should().BeFalse();

        var detailResponse = await _client.GetAsync($"/api/trivias/{triviaId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await detailResponse.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        detail.Should().NotBeNull();
        detail!.Status.Should().Be("Archived");
        detail.HasUsageHistory.Should().BeTrue();

        var catalogResponse = await _client.GetAsync("/api/trivias/");
        catalogResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var catalog = await catalogResponse.Content.ReadFromJsonAsync<IReadOnlyList<TriviasController.TriviaQuizSummaryResponse>>();
        catalog.Should().NotBeNull();
        catalog!.Should().ContainSingle(item =>
            item.Id == triviaId &&
            item.Status == "Archived" &&
            item.HasUsageHistory &&
            item.SourceTriviaQuizId == null &&
            !item.IsDuplicate);
    }

    [Fact]
    public async Task PublishTriviaQuiz_WithNonAdminCaller_Returns403()
    {
        AddAdministratorHeaders();

        var triviaId = await CreateTriviaQuizAsync("Unauthorized Publish");

        AddOperatorHeaders();

        var response = await _client.PostAsync($"/api/trivias/{triviaId}/publish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("Forbidden.");
    }

    [Fact]
    public async Task ArchiveTriviaQuiz_WithNonAdminCaller_Returns403()
    {
        AddAdministratorHeaders();

        var triviaId = await CreateTriviaQuizAsync("Unauthorized Archive");
        await PublishTriviaQuizAsync(triviaId);

        AddOperatorHeaders();

        var response = await _client.PostAsync($"/api/trivias/{triviaId}/archive", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("Forbidden.");
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
                        scoreValue = 100,
                        timeLimitSeconds = 30,
                        explanation = "Caracas is the capital city.",
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

        var payload = await response.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        payload.Should().NotBeNull();

        return payload!.Id;
    }

    private async Task<int> CreateEmptyTriviaQuizAsync(string title)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/trivias/",
            new
            {
                title,
                description = "Trivia briefing.",
                questions = Array.Empty<object>()
            });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        payload.Should().NotBeNull();

        return payload!.Id;
    }

    private async Task<int> GetFirstQuestionIdAsync(int triviaId)
    {
        var detailResponse = await _client.GetAsync($"/api/trivias/{triviaId}");
        detailResponse.EnsureSuccessStatusCode();

        var detail = await detailResponse.Content.ReadFromJsonAsync<TriviasController.TriviaQuizResponse>();
        detail.Should().NotBeNull();

        return detail!.Questions.Single().Id;
    }

    private async Task MarkTriviaQuizAsPublishedAsync(int triviaId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var triviaQuiz = await dbContext.TriviaQuizzes.SingleAsync(quiz => quiz.Id == triviaId);

        triviaQuiz.MarkAsPublished();
        await dbContext.SaveChangesAsync();
    }

    private async Task MarkTriviaQuizAsUsedAsync(int triviaId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var triviaQuiz = await dbContext.TriviaQuizzes.SingleAsync(quiz => quiz.Id == triviaId);

        triviaQuiz.MarkAsUsedInSession();
        await dbContext.SaveChangesAsync();
    }

    private async Task PublishTriviaQuizAsync(int triviaId)
    {
        var response = await _client.PostAsync($"/api/trivias/{triviaId}/publish", content: null);
        response.EnsureSuccessStatusCode();
    }
}
