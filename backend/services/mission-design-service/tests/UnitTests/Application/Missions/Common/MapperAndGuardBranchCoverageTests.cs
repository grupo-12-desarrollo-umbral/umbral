using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Application.Missions.Common;

public sealed class MapperAndGuardBranchCoverageTests
{
    // ── MissionDtoMapper branches ─────────────────────────────────────────────

    [Fact]
    public void MapMission_WithTriviaSubstage_MapsTriviaQuizSelection()
    {
        var mission = Mission.Create("M", "D", "Advanced", 45);
        var stage = mission.AddStage("S", 1);
        stage.Id = 1;
        var triviaSubstage = mission.AddSubstage(stage.Id, Substage.CreateTrivia("Trivia", 1));
        triviaSubstage.Id = 2;
        triviaSubstage.SelectTriviaQuiz(42);

        var dto = MissionDtoMapper.Map(mission);

        var substageDto = dto.Stages.Single().Substages.Single();
        substageDto.TriviaQuizSelection.Should().NotBeNull();
        substageDto.TriviaQuizSelection!.TriviaQuizId.Should().Be(42);
    }

    [Fact]
    public void MapMission_WithTreasureSubstage_MapsNullTriviaQuizSelection()
    {
        var mission = Mission.Create("M", "D", "Advanced", 45);
        var stage = mission.AddStage("S", 1);
        stage.Id = 1;
        var treasureSubstage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Hunt", 1));
        treasureSubstage.Id = 2;

        var dto = MissionDtoMapper.Map(mission);

        var substageDto = dto.Stages.Single().Substages.Single();
        substageDto.TriviaQuizSelection.Should().BeNull();
    }

    [Fact]
    public void MapMission_WithTargets_MapsTargetDetails()
    {
        var mission = Mission.Create("M", "D", "Beginner", 30);
        var stage = mission.AddStage("S", 1);
        stage.Id = 1;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Hunt", 1));
        substage.Id = 2;
        var target = mission.AddTarget(1, 2, "Statue", "QR-1", 1, 4.711, -74.0721, isActive: true);
        target.Id = 10;

        var dto = MissionDtoMapper.Map(mission);

        var targetDto = dto.Stages.Single().Substages.Single().Targets.Single();
        targetDto.Name.Should().Be("Statue");
        targetDto.QrCode.Should().Be("QR-1");
        targetDto.IsActive.Should().BeTrue();
        targetDto.Score.Should().Be(50); // Beginner => 50 * 1
    }

    [Fact]
    public void MapMission_WithClues_MapsClueDetails()
    {
        var mission = Mission.Create("M", "D", "Advanced", 45);
        var stage = mission.AddStage("S", 1);
        stage.Id = 1;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Hunt", 1));
        substage.Id = 2;
        var clue = mission.AddClue(1, 2, Clue.Create("Hint", 1, "Look north", ClueVisibilityPolicy.VisibleWhenSubstageStarts));
        clue.Id = 5;

        var dto = MissionDtoMapper.Map(mission);

        var clueDto = dto.Stages.Single().Substages.Single().Clues.Single();
        clueDto.Title.Should().Be("Hint");
        clueDto.Text.Should().Be("Look north");
        clueDto.VisibilityPolicy.Should().Be("VisibleWhenSubstageStarts");
    }

    [Fact]
    public void MapMission_MultipleStagesOrderedBySequence()
    {
        var mission = Mission.Create("M", "D", "Advanced", 45);
        var stage2 = mission.AddStage("Second", 2);
        stage2.Id = 2;
        var stage1 = mission.AddStage("First", 1);
        stage1.Id = 1;

        var dto = MissionDtoMapper.Map(mission);

        dto.Stages.Select(s => s.Title).Should().ContainInOrder("First", "Second");
    }

    // ── TriviaQuizDtoMapper branches ─────────────────────────────────────────

    [Fact]
    public void MapTriviaQuiz_WithQuestionsAndOptions_MapsAllFields()
    {
        var quiz = TriviaQuiz.Create("Quiz", "Description");
        var question = quiz.AddQuestion("Prompt", 50, 30, "Explanation", [
            TriviaOption.Create("A", 1, true),
            TriviaOption.Create("B", 2, false)
        ]);
        question.Id = 10;
        quiz.Publish(DateTimeOffset.UtcNow);

        var dto = TriviaQuizDtoMapper.Map(quiz);

        dto.Title.Should().Be("Quiz");
        dto.Description.Should().Be("Description");
        dto.Status.Should().Be("Published");
        dto.Questions.Should().ContainSingle();
        var q = dto.Questions.Single();
        q.Prompt.Should().Be("Prompt");
        q.ScoreValue.Should().Be(50);
        q.TimeLimitSeconds.Should().Be(30);
        q.Explanation.Should().Be("Explanation");
        q.Options.Should().HaveCount(2);
    }

    [Fact]
    public void MapTriviaQuiz_WhenDuplicate_SetsIsDuplicateTrue()
    {
        var original = TriviaQuiz.Create("Q", "D");
        original.AddQuestion("P", 10, 30, null, [
            TriviaOption.Create("A", 1, true),
            TriviaOption.Create("B", 2, false)
        ]);
        original.Id = 41;
        original.Publish(DateTimeOffset.UtcNow);

        var duplicate = original.Duplicate();

        var dto = TriviaQuizDtoMapper.Map(duplicate);

        dto.IsDuplicate.Should().BeTrue();
        dto.SourceTriviaQuizId.Should().Be(41);
        dto.HasUsageHistory.Should().BeFalse();
    }

    [Fact]
    public void MapTriviaQuiz_QuestionsWithUnsavedIdentityPreserveInsertionOrder()
    {
        var quiz = TriviaQuiz.Create("Q", "D");
        quiz.AddQuestion("Second", 10, 30, null, [
            TriviaOption.Create("A", 1, true),
            TriviaOption.Create("B", 2, false)
        ]);
        quiz.AddQuestion("First", 10, 30, null, [
            TriviaOption.Create("A", 1, true),
            TriviaOption.Create("B", 2, false)
        ]);

        var dto = TriviaQuizDtoMapper.Map(quiz);

        dto.Questions.Select(q => q.Prompt).Should().ContainInOrder("Second", "First");
    }

    // ── MissionTriviaPublicationChecker.Evaluate branches ─────────────────────

    [Fact]
    public void Evaluate_WhenSubstageIsTreasureHunt_SkipsTriviaCheck()
    {
        var mission = Mission.Create("M", "D", "Advanced", 45);
        var stage = mission.AddStage("S", 1);
        stage.Id = 1;
        var treasure = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Hunt", 1));
        treasure.Id = 2;

        var failures = MissionTriviaPublicationChecker.Evaluate(mission, new Dictionary<int, TriviaQuizStatus>());

        failures.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_WhenSubstageIsTriviaWithoutSelection_SkipsTriviaCheck()
    {
        var mission = Mission.Create("M", "D", "Advanced", 45);
        var stage = mission.AddStage("S", 1);
        stage.Id = 1;
        var trivia = mission.AddSubstage(stage.Id, Substage.CreateTrivia("Trivia", 1));
        trivia.Id = 2;
        // No SelectTriviaQuiz call

        var failures = MissionTriviaPublicationChecker.Evaluate(mission, new Dictionary<int, TriviaQuizStatus>());

        failures.Should().BeEmpty();
    }

    [Fact]
    public void CollectTriviaQuizIds_WhenNoTriviaSubstages_ReturnsEmpty()
    {
        var mission = Mission.Create("M", "D", "Advanced", 45);
        var stage = mission.AddStage("S", 1);
        stage.Id = 1;
        var treasure = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Hunt", 1));
        treasure.Id = 2;

        MissionTriviaPublicationChecker.CollectTriviaQuizIds(mission).Should().BeEmpty();
    }

    [Fact]
    public void CollectTriviaQuizIds_WhenMultipleSubstagesReferenceSameQuiz_ReturnsDistinct()
    {
        var mission = Mission.Create("M", "D", "Advanced", 45);
        var stage = mission.AddStage("S", 1);
        stage.Id = 1;
        var trivia1 = mission.AddSubstage(stage.Id, Substage.CreateTrivia("T1", 1));
        trivia1.Id = 2;
        trivia1.SelectTriviaQuiz(42);
        var trivia2 = mission.AddSubstage(stage.Id, Substage.CreateTrivia("T2", 2));
        trivia2.Id = 3;
        trivia2.SelectTriviaQuiz(42);

        MissionTriviaPublicationChecker.CollectTriviaQuizIds(mission).Should().ContainSingle().Which.Should().Be(42);
    }

    // ── ActiveMissionTriviaReferenceGuard branches ───────────────────────────

    [Fact]
    public async Task EnsureNotReferenced_WhenNoReferences_DoesNotThrow()
    {
        var repo = new TestMissionRepository();

        var act = () => ActiveMissionTriviaReferenceGuard.EnsureNotReferencedByActiveMissionAsync(
            repo, 42, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureNotReferenced_WhenReferencesExist_ThrowsTriviaQuizReferencedByActiveMissionException()
    {
        var repo = new TestMissionRepository();
        repo.AddReference(new ActiveMissionReference(1, "Mission A"));

        var act = () => ActiveMissionTriviaReferenceGuard.EnsureNotReferencedByActiveMissionAsync(
            repo, 42, CancellationToken.None);

        await act.Should().ThrowAsync<TriviaQuizReferencedByActiveMissionException>();
    }

    private sealed class TestMissionRepository : IMissionRepository
    {
        private readonly List<ActiveMissionReference> _references = [];

        public void AddReference(ActiveMissionReference reference) => _references.Add(reference);

        public Task<IReadOnlyList<ActiveMissionReference>> GetActiveMissionsReferencingTriviaQuizAsync(
            int triviaQuizId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ActiveMissionReference>>(_references);
        }

        public Task<Mission?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult<Mission?>(null);
        public Task AddAsync(Mission mission, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task UpdateAsync(Mission mission, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
