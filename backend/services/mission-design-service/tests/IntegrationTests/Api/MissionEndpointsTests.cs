using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Web.Endpoints;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

[Collection("MissionDesignIntegrationTests")]
public sealed class MissionEndpointsTests : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private MissionDesignApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public MissionEndpointsTests(PostgreSqlFixture fixture)
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
    public async Task CreateMission_ReturnsCreatedMissionWithDraftSourceReadiness()
    {
        AddAdministratorHeaders();

        var response = await _client.PostAsJsonAsync(
            "/api/missions/",
            new
            {
                name = "Mission Atlas",
                description = "Locate the relay point.",
                difficulty = "Advanced",
                maximumTimeMinutes = 50
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var payload = await response.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        payload.Should().NotBeNull();
        payload!.Name.Should().Be("Mission Atlas");
        payload.Description.Should().Be("Locate the relay point.");
        payload.Difficulty.Should().Be("Advanced");
        payload.MaximumTimeMinutes.Should().Be(50);
        payload.IsActive.Should().BeTrue();
        payload.ActivationState.Should().Be("Draft");
        payload.IsSourceReady.Should().BeFalse();
    }

    [Fact]
    public async Task GetMissionCatalogAndDetail_ReturnInactiveMissionAsUnavailableForNewSessions()
    {
        AddAdministratorHeaders();

        var missionId = await CreateMissionAsync("Mission Atlas");

        var deactivateResponse = await _client.DeleteAsync($"/api/missions/{missionId}");
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var catalogResponse = await _client.GetAsync("/api/missions/");
        catalogResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var catalog = await catalogResponse.Content.ReadFromJsonAsync<IReadOnlyList<MissionsEndpoints.MissionSummaryResponse>>();
        catalog.Should().NotBeNull();
        catalog!.Should().ContainSingle();
        catalog[0].Id.Should().Be(missionId);
        catalog[0].ActivationState.Should().Be("Inactive");
        catalog[0].IsActive.Should().BeFalse();
        catalog[0].IsSourceReady.Should().BeFalse();

        var detailResponse = await _client.GetAsync($"/api/missions/{missionId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await detailResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(missionId);
        detail.ActivationState.Should().Be("Inactive");
        detail.IsActive.Should().BeFalse();
        detail.IsSourceReady.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateMission_ReturnsUpdatedMission()
    {
        AddAdministratorHeaders();

        var missionId = await CreateMissionAsync("Mission Before");

        var response = await _client.PutAsJsonAsync(
            $"/api/missions/{missionId}",
            new
            {
                name = "Mission After",
                description = "Updated briefing.",
                difficulty = "Beginner",
                maximumTimeMinutes = 25
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        payload.Should().NotBeNull();
        payload!.Id.Should().Be(missionId);
        payload.Name.Should().Be("Mission After");
        payload.Description.Should().Be("Updated briefing.");
        payload.Difficulty.Should().Be("Beginner");
        payload.MaximumTimeMinutes.Should().Be(25);
        payload.ActivationState.Should().Be("Draft");
        payload.IsSourceReady.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateMission_WithInvalidPayload_ReturnsBadRequest()
    {
        AddAdministratorHeaders();

        var missionId = await CreateMissionAsync("Mission Before");

        var response = await _client.PutAsJsonAsync(
            $"/api/missions/{missionId}",
            new
            {
                name = "",
                description = "",
                difficulty = "",
                maximumTimeMinutes = 0
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Validation failed.");
    }

    [Fact]
    public async Task UpdateMission_WhenMissionDoesNotExist_ReturnsNotFound()
    {
        AddAdministratorHeaders();

        var response = await _client.PutAsJsonAsync(
            "/api/missions/999",
            new
            {
                name = "Missing Mission",
                description = "Missing briefing.",
                difficulty = "Advanced",
                maximumTimeMinutes = 45
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Title.Should().Be("Resource not found.");
    }

    [Fact]
    public async Task DeactivateMission_WhenMissionDoesNotExist_ReturnsNotFound()
    {
        AddAdministratorHeaders();

        var response = await _client.DeleteAsync("/api/missions/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Title.Should().Be("Resource not found.");
    }

    [Fact]
    public async Task DeactivateMission_WithInvalidRouteValue_ReturnsBadRequest()
    {
        AddAdministratorHeaders();

        var response = await _client.DeleteAsync("/api/missions/0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Validation failed.");
    }

    [Fact]
    public async Task DeactivateMission_WhenMissionIsAlreadyInactive_ReturnsConflict()
    {
        AddAdministratorHeaders();

        var missionId = await CreateMissionAsync("Double Deactivate Mission");

        var firstResponse = await _client.DeleteAsync($"/api/missions/{missionId}");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondResponse = await _client.DeleteAsync($"/api/missions/{missionId}");

        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await secondResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Mission cannot change activation state from its current state.");
    }

    [Fact]
    public async Task MissionAuthoringEndpoints_BuildTreeExposeReadinessAndActivateReadyMission()
    {
        AddAdministratorHeaders();

        var missionId = await CreateMissionAsync("Mission Runtime Plan");

        var addStageResponse = await _client.PostAsJsonAsync(
            $"/api/missions/{missionId}/nodes",
            new
            {
                nodeType = "Stage",
                title = "Stage 1",
                sequenceOrder = 1
            });
        addStageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var missionAfterStage = await addStageResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        missionAfterStage.Should().NotBeNull();
        var stageId = missionAfterStage!.Stages.Single().Id;

        var addSubstageResponse = await _client.PostAsJsonAsync(
            $"/api/missions/{missionId}/nodes",
            new
            {
                nodeType = "Substage",
                title = "Treasure Hunt",
                sequenceOrder = 1,
                stageId,
                playMode = "TreasureHunt"
            });
        addSubstageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var missionAfterSubstage = await addSubstageResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        missionAfterSubstage.Should().NotBeNull();
        var substageId = missionAfterSubstage!.Stages.Single().Substages.Single().Id;

        var readinessBeforeTargetsResponse = await _client.GetAsync($"/api/missions/{missionId}/readiness");
        readinessBeforeTargetsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var readinessBeforeTargets = await readinessBeforeTargetsResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionReadinessResponse>();
        readinessBeforeTargets.Should().NotBeNull();
        readinessBeforeTargets!.IsReady.Should().BeFalse();
        readinessBeforeTargets.Failures.Should().Contain(failure => failure.Contains("must have at least one active target", StringComparison.Ordinal));
        readinessBeforeTargets.Failures.Should().Contain(failure => failure.Contains("must define a winner score", StringComparison.Ordinal));

        var addClueResponse = await _client.PostAsJsonAsync(
            $"/api/missions/{missionId}/nodes",
            new
            {
                nodeType = "Clue",
                title = "Clue 1",
                sequenceOrder = 1,
                stageId,
                substageId,
                clueText = "Look near the old gate."
            });
        addClueResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var missionAfterClue = await addClueResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        missionAfterClue.Should().NotBeNull();
        var clueId = missionAfterClue!.Stages.Single().Substages.Single().Clues.Single().Id;

        var addTargetResponse = await _client.PostAsJsonAsync(
            $"/api/missions/{missionId}/stages/{stageId}/substages/{substageId}/targets",
            new
            {
                name = "Target 1",
                qrCode = "QR-001",
                sequenceOrder = 1,
                isActive = true,
                winnerScore = 35
            });
        addTargetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var missionAfterTarget = await addTargetResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        missionAfterTarget.Should().NotBeNull();
        var targetId = missionAfterTarget!.Stages.Single().Substages.Single().Targets.Single().Id;
        missionAfterTarget.Stages.Single().Substages.Single().WinnerScore.Should().Be(35);

        var associateClueResponse = await _client.PostAsJsonAsync(
            $"/api/missions/{missionId}/stages/{stageId}/substages/{substageId}/targets/{targetId}/clue-association",
            new
            {
                clueId
            });
        associateClueResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detailResponse = await _client.GetAsync($"/api/missions/{missionId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await detailResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        detail.Should().NotBeNull();
        detail!.Stages.Should().ContainSingle();
        detail.Stages[0].Substages.Should().ContainSingle();
        detail.Stages[0].Substages[0].PlayMode.Should().Be("TreasureHunt");
        detail.Stages[0].Substages[0].Targets.Should().ContainSingle();
        detail.Stages[0].Substages[0].Targets[0].ClueId.Should().Be(clueId);
        detail.Stages[0].Substages[0].Clues.Should().ContainSingle();

        var activateResponse = await _client.PostAsync($"/api/missions/{missionId}/activate", content: null);
        activateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var activatedMission = await activateResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        activatedMission.Should().NotBeNull();
        activatedMission!.ActivationState.Should().Be("Ready");
        activatedMission.IsSourceReady.Should().BeTrue();
    }

    [Fact]
    public async Task MissionNodeEndpoints_UpdateAndDeleteNodes_ReturnUpdatedStructure()
    {
        AddAdministratorHeaders();

        var missionId = await CreateMissionAsync("Node Editing Mission");
        var (stageId, _) = await CreateTreasureHuntStructureAsync(missionId);
        var removableStageId = await AddStageAsync(missionId, "Removable Stage", 2);

        var updateStageResponse = await _client.PutAsJsonAsync(
            $"/api/missions/{missionId}/nodes/{stageId}",
            new
            {
                title = "Updated Stage",
                sequenceOrder = 3
            });
        updateStageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var missionAfterStageUpdate = await updateStageResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        missionAfterStageUpdate.Should().NotBeNull();
        missionAfterStageUpdate!.Stages.Single(stage => stage.Id == stageId).Title.Should().Be("Updated Stage");
        missionAfterStageUpdate.Stages.Single(stage => stage.Id == stageId).SequenceOrder.Should().Be(3);

        var deleteStageResponse = await _client.DeleteAsync($"/api/missions/{missionId}/nodes/{removableStageId}");
        deleteStageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var missionAfterStageDelete = await deleteStageResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        missionAfterStageDelete.Should().NotBeNull();
        missionAfterStageDelete!.Stages.Should().ContainSingle(stage => stage.Id == stageId);
        missionAfterStageDelete.Stages.Should().NotContain(stage => stage.Id == removableStageId);
    }

    [Fact]
    public async Task MissionStructureEndpoints_AssignPlayModeUpdateDeleteTargetAndUnassociateClue()
    {
        AddAdministratorHeaders();

        var missionId = await CreateMissionAsync("Treasure Editing Mission");
        var (stageId, substageId) = await CreateTriviaStructureAsync(missionId);
        await AddClueAsync(missionId, stageId, substageId, "Guidance Clue", 1);

        var assignPlayModeResponse = await _client.PutAsJsonAsync(
            $"/api/missions/{missionId}/stages/{stageId}/substages/{substageId}/play-mode",
            new
            {
                playMode = "TreasureHunt"
            });
        assignPlayModeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var missionAfterPlayModeAssign = await assignPlayModeResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        missionAfterPlayModeAssign.Should().NotBeNull();
        var treasureSubstage = missionAfterPlayModeAssign!.Stages.Single().Substages.Single();
        treasureSubstage.PlayMode.Should().Be("TreasureHunt");
        treasureSubstage.Clues.Should().BeEmpty();

        var clueId = await AddClueAsync(missionId, stageId, substageId, "Post-Switch Clue", 1);
        var targetId = await AddTargetAsync(missionId, stageId, substageId);

        var updateTargetResponse = await _client.PutAsJsonAsync(
            $"/api/missions/{missionId}/stages/{stageId}/substages/{substageId}/targets/{targetId}",
            new
            {
                name = "Updated Target",
                qrCode = "QR-UPDATED",
                sequenceOrder = 4,
                isActive = false,
                winnerScore = 50
            });
        updateTargetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var missionAfterTargetUpdate = await updateTargetResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        missionAfterTargetUpdate.Should().NotBeNull();
        var updatedTarget = missionAfterTargetUpdate!.Stages.Single().Substages.Single().Targets.Single();
        updatedTarget.Name.Should().Be("Updated Target");
        updatedTarget.QrCode.Should().Be("QR-UPDATED");
        updatedTarget.SequenceOrder.Should().Be(4);
        updatedTarget.IsActive.Should().BeFalse();
        missionAfterTargetUpdate.Stages.Single().Substages.Single().WinnerScore.Should().Be(50);

        var associateClueResponse = await _client.PostAsJsonAsync(
            $"/api/missions/{missionId}/stages/{stageId}/substages/{substageId}/targets/{targetId}/clue-association",
            new
            {
                clueId
            });
        associateClueResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var unassociateClueResponse = await _client.DeleteAsync(
            $"/api/missions/{missionId}/stages/{stageId}/substages/{substageId}/targets/{targetId}/clue-association");
        unassociateClueResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var missionAfterClueUnassociation = await unassociateClueResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        missionAfterClueUnassociation.Should().NotBeNull();
        missionAfterClueUnassociation!.Stages.Single().Substages.Single().Targets.Single().ClueId.Should().BeNull();

        var deleteTargetResponse = await _client.DeleteAsync(
            $"/api/missions/{missionId}/stages/{stageId}/substages/{substageId}/targets/{targetId}");
        deleteTargetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var missionAfterTargetDelete = await deleteTargetResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        missionAfterTargetDelete.Should().NotBeNull();
        missionAfterTargetDelete!.Stages.Single().Substages.Single().Targets.Should().BeEmpty();
    }

    [Fact]
    public async Task AssignPlayMode_WithLegacyDifficulty_AllowsSwitchToTrivia()
    {
        AddAdministratorHeaders();

        var missionId = await CreateMissionAsync("Legacy Difficulty Mission");
        var stageId = await AddStageAsync(missionId, "Stage 1", 1);
        var substageId = await AddSubstageAsync(missionId, stageId, "Treasure Hunt", "TreasureHunt", 1);
        await AddTargetAsync(missionId, stageId, substageId);
        await SetMissionDifficultyAsync(missionId, "Easy");

        var assignPlayModeResponse = await _client.PutAsJsonAsync(
            $"/api/missions/{missionId}/stages/{stageId}/substages/{substageId}/play-mode",
            new
            {
                playMode = "Trivia"
            });
        assignPlayModeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var missionAfterPlayModeAssign = await assignPlayModeResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        missionAfterPlayModeAssign.Should().NotBeNull();
        var triviaSubstage = missionAfterPlayModeAssign!.Stages.Single().Substages.Single();
        missionAfterPlayModeAssign.Difficulty.Should().Be("Easy");
        triviaSubstage.PlayMode.Should().Be("Trivia");
        triviaSubstage.Targets.Should().BeEmpty();
        triviaSubstage.WinnerScore.Should().BeNull();
    }

    [Fact]
    public async Task MissionTriviaQuizSelectionEndpoints_SetAndUpdateSelection()
    {
        AddAdministratorHeaders();

        var firstTriviaQuizId = await CreatePublishedTriviaQuizAsync("First Published Quiz");
        var secondTriviaQuizId = await CreatePublishedTriviaQuizAsync("Second Published Quiz");
        var missionId = await CreateMissionAsync("Trivia Selection Mission");
        var (stageId, substageId) = await CreateTriviaStructureAsync(missionId);

        var setSelectionResponse = await _client.PostAsJsonAsync(
            $"/api/missions/{missionId}/stages/{stageId}/substages/{substageId}/trivia-quiz-selection",
            new
            {
                triviaQuizId = firstTriviaQuizId
            });
        setSelectionResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var missionAfterSelectionSet = await setSelectionResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        missionAfterSelectionSet.Should().NotBeNull();
        missionAfterSelectionSet!.Stages.Single().Substages.Single().TriviaQuizSelection.Should().NotBeNull();
        missionAfterSelectionSet.Stages.Single().Substages.Single().TriviaQuizSelection!.TriviaQuizId.Should().Be(firstTriviaQuizId);

        var updateSelectionResponse = await _client.PutAsJsonAsync(
            $"/api/missions/{missionId}/stages/{stageId}/substages/{substageId}/trivia-quiz-selection",
            new
            {
                triviaQuizId = secondTriviaQuizId
            });
        updateSelectionResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var missionAfterSelectionUpdate = await updateSelectionResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        missionAfterSelectionUpdate.Should().NotBeNull();
        missionAfterSelectionUpdate!.Stages.Single().Substages.Single().TriviaQuizSelection.Should().NotBeNull();
        missionAfterSelectionUpdate.Stages.Single().Substages.Single().TriviaQuizSelection!.TriviaQuizId.Should().Be(secondTriviaQuizId);
    }

    [Fact]
    public async Task GetMissionRuntimePlan_ReturnsResolvedMixedModeMissionInStrictOrder()
    {
        AddAdministratorHeaders();

        var createTriviaResponse = await _client.PostAsJsonAsync(
            "/api/trivias/",
            new
            {
                title = "Runtime Trivia",
                description = "Resolved quiz for runtime consumers.",
                questions = new[]
                {
                    new
                    {
                        prompt = "Second question",
                        sequenceOrder = 2,
                        scoreValue = 35,
                        timeLimitSeconds = 20,
                        explanation = "Second explanation.",
                        isActive = true,
                        options = new[]
                        {
                            new { optionText = "Wrong", sequenceOrder = 2, isCorrect = false },
                            new { optionText = "Right", sequenceOrder = 1, isCorrect = true }
                        }
                    },
                    new
                    {
                        prompt = "First question",
                        sequenceOrder = 1,
                        scoreValue = 50,
                        timeLimitSeconds = 30,
                        explanation = "First explanation.",
                        isActive = true,
                        options = new[]
                        {
                            new { optionText = "First correct", sequenceOrder = 1, isCorrect = true },
                            new { optionText = "First wrong", sequenceOrder = 2, isCorrect = false }
                        }
                    }
                }
            });
        createTriviaResponse.EnsureSuccessStatusCode();

        var createdTriviaQuiz = await createTriviaResponse.Content.ReadFromJsonAsync<TriviasEndpoints.TriviaQuizResponse>();
        createdTriviaQuiz.Should().NotBeNull();

        var publishTriviaResponse = await _client.PostAsync($"/api/trivias/{createdTriviaQuiz!.Id}/publish", content: null);
        publishTriviaResponse.EnsureSuccessStatusCode();

        var missionId = await CreateMissionAsync("Mission Runtime Plan");

        var triviaStageId = await AddStageAsync(missionId, "Stage 2", 2);
        var triviaSubstageId = await AddSubstageAsync(missionId, triviaStageId, "Trivia Round", "Trivia", 1);

        var setTriviaSelectionResponse = await _client.PostAsJsonAsync(
            $"/api/missions/{missionId}/stages/{triviaStageId}/substages/{triviaSubstageId}/trivia-quiz-selection",
            new
            {
                triviaQuizId = createdTriviaQuiz.Id
            });
        setTriviaSelectionResponse.EnsureSuccessStatusCode();

        var treasureStageId = await AddStageAsync(missionId, "Stage 1", 1);
        var treasureSubstageId = await AddSubstageAsync(missionId, treasureStageId, "Treasure Hunt", "TreasureHunt", 1);

        var addClueResponse = await _client.PostAsJsonAsync(
            $"/api/missions/{missionId}/nodes",
            new
            {
                nodeType = "Clue",
                title = "Treasure Clue",
                sequenceOrder = 1,
                stageId = treasureStageId,
                substageId = treasureSubstageId,
                clueText = "Look beneath the arch.",
                clueVisibilityPolicy = "VisibleWhenSubstageStarts"
            });
        addClueResponse.EnsureSuccessStatusCode();

        var missionAfterClue = await addClueResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        missionAfterClue.Should().NotBeNull();
        var clueId = missionAfterClue!.Stages
            .Single(stage => stage.Id == treasureStageId)
            .Substages.Single(substage => substage.Id == treasureSubstageId)
            .Clues.Single()
            .Id;

        var addTargetResponse = await _client.PostAsJsonAsync(
            $"/api/missions/{missionId}/stages/{treasureStageId}/substages/{treasureSubstageId}/targets",
            new
            {
                name = "Beacon",
                qrCode = "QR-BEACON",
                sequenceOrder = 1,
                isActive = true,
                winnerScore = 40
            });
        addTargetResponse.EnsureSuccessStatusCode();

        var missionAfterTarget = await addTargetResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        missionAfterTarget.Should().NotBeNull();
        var targetId = missionAfterTarget!.Stages
            .Single(stage => stage.Id == treasureStageId)
            .Substages.Single(substage => substage.Id == treasureSubstageId)
            .Targets.Single()
            .Id;

        var associateClueResponse = await _client.PostAsJsonAsync(
            $"/api/missions/{missionId}/stages/{treasureStageId}/substages/{treasureSubstageId}/targets/{targetId}/clue-association",
            new
            {
                clueId
            });
        associateClueResponse.EnsureSuccessStatusCode();

        var activateMissionResponse = await _client.PostAsync($"/api/missions/{missionId}/activate", content: null);
        activateMissionResponse.EnsureSuccessStatusCode();

        var runtimePlanResponse = await _client.GetAsync($"/api/missions/{missionId}/runtime-plan");
        runtimePlanResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var runtimePlan = await runtimePlanResponse.Content.ReadFromJsonAsync<MissionsEndpoints.MissionRuntimePlanResponse>();
        runtimePlan.Should().NotBeNull();
        runtimePlan!.Title.Should().Be("Mission Runtime Plan");
        runtimePlan.MaximumTime.Should().Be(45);
        runtimePlan.Stages.Should().HaveCount(2);

        var treasureSubstage = runtimePlan.Stages[0].Substages.Should().ContainSingle().Which;
        treasureSubstage.PlayMode.Should().Be("TreasureHunt");
        treasureSubstage.WinnerScore.Should().Be(40);
        treasureSubstage.TriviaQuestions.Should().BeEmpty();
        treasureSubstage.Targets.Should().ContainSingle();
        treasureSubstage.Targets[0].Name.Should().Be("Beacon");
        treasureSubstage.Targets[0].QrCode.Should().Be("QR-BEACON");
        treasureSubstage.Targets[0].SequenceOrder.Should().Be(1);
        treasureSubstage.Targets[0].IsActive.Should().BeTrue();
        treasureSubstage.Targets[0].Clue.Should().NotBeNull();
        treasureSubstage.Targets[0].Clue!.Text.Should().Be("Look beneath the arch.");
        treasureSubstage.Targets[0].Clue!.VisibilityPolicy.Should().Be("VisibleWhenSubstageStarts");

        var triviaSubstage = runtimePlan.Stages[1].Substages.Should().ContainSingle().Which;
        triviaSubstage.PlayMode.Should().Be("Trivia");
        triviaSubstage.WinnerScore.Should().BeNull();
        triviaSubstage.Targets.Should().BeEmpty();
        triviaSubstage.TriviaQuestions.Select(question => question.Prompt).Should().Equal("First question", "Second question");
        triviaSubstage.TriviaQuestions.Select(question => question.ScoreValue).Should().Equal(50, 35);
        triviaSubstage.TriviaQuestions.Select(question => question.TimeLimitSeconds).Should().Equal(30, 20);
        triviaSubstage.TriviaQuestions[0].Options.Select(option => option.OptionText).Should().Equal("First correct", "First wrong");
        triviaSubstage.TriviaQuestions[0].Options.Select(option => option.IsCorrect).Should().Equal(true, false);
        triviaSubstage.TriviaQuestions[1].Options.Select(option => option.OptionText).Should().Equal("Right", "Wrong");
        triviaSubstage.TriviaQuestions[1].Options.Select(option => option.IsCorrect).Should().Equal(true, false);
    }

    [Fact]
    public async Task ActivateMission_WhenRuntimePlanIsIncomplete_ReturnsBadRequest()
    {
        AddAdministratorHeaders();

        var missionId = await CreateMissionAsync("Incomplete Mission");

        var response = await _client.PostAsync($"/api/missions/{missionId}/activate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Validation failed.");
    }

    private void AddAdministratorHeaders()
    {
        _client.DefaultRequestHeaders.Remove("X-User-Id");
        _client.DefaultRequestHeaders.Remove("X-User-Role");
        _client.DefaultRequestHeaders.Add("X-User-Id", "admin-01");
        _client.DefaultRequestHeaders.Add("X-User-Role", "Administrator");
    }

    private async Task<int> CreateMissionAsync(string name)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/missions/",
            new
            {
                name,
                description = "Mission briefing.",
                difficulty = "Advanced",
                maximumTimeMinutes = 45
            });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        payload.Should().NotBeNull();

        return payload!.Id;
    }

    private async Task<(int StageId, int SubstageId)> CreateTreasureHuntStructureAsync(int missionId)
    {
        var stageId = await AddStageAsync(missionId, "Stage 1", 1);
        var substageId = await AddSubstageAsync(missionId, stageId, "Treasure Hunt", "TreasureHunt", 1);

        return (stageId, substageId);
    }

    private async Task<(int StageId, int SubstageId)> CreateTriviaStructureAsync(int missionId)
    {
        var stageId = await AddStageAsync(missionId, "Stage 1", 1);
        var substageId = await AddSubstageAsync(missionId, stageId, "Trivia", "Trivia", 1);

        return (stageId, substageId);
    }

    private async Task<int> AddStageAsync(int missionId, string title, int sequenceOrder)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/missions/{missionId}/nodes",
            new
            {
                nodeType = "Stage",
                title,
                sequenceOrder
            });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        payload.Should().NotBeNull();

        return payload!.Stages.Single(stage => stage.Title == title).Id;
    }

    private async Task<int> AddSubstageAsync(
        int missionId,
        int stageId,
        string title,
        string playMode,
        int sequenceOrder)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/missions/{missionId}/nodes",
            new
            {
                nodeType = "Substage",
                title,
                sequenceOrder,
                stageId,
                playMode
            });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        payload.Should().NotBeNull();

        return payload!.Stages.Single(stage => stage.Id == stageId).Substages.Single(substage => substage.Title == title).Id;
    }

    private async Task<int> AddClueAsync(int missionId, int stageId, int substageId, string title, int sequenceOrder)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/missions/{missionId}/nodes",
            new
            {
                nodeType = "Clue",
                title,
                sequenceOrder,
                stageId,
                substageId,
                clueText = "Look near the old gate."
            });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        payload.Should().NotBeNull();

        return payload!.Stages.Single().Substages.Single().Clues.Single(clue => clue.Title == title).Id;
    }

    private async Task<int> AddTargetAsync(int missionId, int stageId, int substageId)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/missions/{missionId}/stages/{stageId}/substages/{substageId}/targets",
            new
            {
                name = "Target 1",
                qrCode = "QR-001",
                sequenceOrder = 1,
                isActive = true,
                winnerScore = 35
            });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MissionsEndpoints.MissionResponse>();
        payload.Should().NotBeNull();

        return payload!.Stages.Single().Substages.Single().Targets.Single(target => target.Name == "Target 1").Id;
    }

    private async Task<int> CreatePublishedTriviaQuizAsync(string title)
    {
        var createResponse = await _client.PostAsJsonAsync(
            "/api/trivias/",
            new
            {
                title,
                description = "Trivia briefing.",
                questions = new[]
                {
                    new
                    {
                        prompt = $"{title} question?",
                        sequenceOrder = 1,
                        scoreValue = 100,
                        timeLimitSeconds = 45,
                        explanation = "Published quiz setup.",
                        isActive = true,
                        options = new[]
                        {
                            new { optionText = "Correct", sequenceOrder = 1, isCorrect = true },
                            new { optionText = "Incorrect", sequenceOrder = 2, isCorrect = false }
                        }
                    }
                }
            });

        createResponse.EnsureSuccessStatusCode();

        var payload = await createResponse.Content.ReadFromJsonAsync<TriviasEndpoints.TriviaQuizResponse>();
        payload.Should().NotBeNull();

        var publishResponse = await _client.PostAsync($"/api/trivias/{payload!.Id}/publish", content: null);
        publishResponse.EnsureSuccessStatusCode();

        return payload.Id;
    }

    private async Task SetMissionDifficultyAsync(int missionId, string difficulty)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Missions\" SET \"Difficulty\" = {difficulty} WHERE \"Id\" = {missionId};");
    }
}
