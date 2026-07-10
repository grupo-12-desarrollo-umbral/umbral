using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Missions.Commands.AssignSubstagePlayMode;
using umbral_backend.Application.Missions.Commands.RemoveMissionNode;
using umbral_backend.Application.Missions.Commands.RemoveTarget;
using umbral_backend.Application.Missions.Commands.UpdateMissionNode;
using umbral_backend.Application.Missions.Commands.UpdateTarget;
using umbral_backend.Application.Missions.Commands.SelectTriviaQuiz;
using umbral_backend.Application.Missions.Commands.ActivateMission;
using umbral_backend.Application.Missions.Commands.AddMissionNode;
using umbral_backend.Application.Missions.Commands.AddTarget;
using umbral_backend.Application.Missions.Commands.AssociateClueWithTarget;
using umbral_backend.Application.Missions.Commands.CreateMission;
using umbral_backend.Application.Missions.Commands.DeactivateMission;
using umbral_backend.Application.Missions.Commands.UnassociateClueFromTarget;
using umbral_backend.Application.Missions.Commands.UpdateMission;
using umbral_backend.Application.Missions.Queries.GetDifficultyCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionDetail;
using umbral_backend.Application.Missions.Queries.GetMissionReadiness;
using umbral_backend.Application.Missions.Queries.GetMissionRuntimePlan;
using umbral_backend.Application.UnitTests.Application.Missions.TestDoubles;
using umbral_backend.Application.UnitTests.Application.Trivias.TestDoubles;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Application.Missions.Handlers;

public sealed class MissionMutationCommandHandlerTests
{
    // ── GetDifficultyCatalog ──────────────────────────────────────────────────

    [Fact]
    public async Task GetDifficultyCatalog_ReturnsAllAllowedValues()
    {
        var handler = new GetDifficultyCatalogQueryHandler();

        var result = await handler.Handle(new GetDifficultyCatalogQuery(), CancellationToken.None);

        result.Should().ContainSingle(dto => dto.Value == "Beginner");
        result.Should().ContainSingle(dto => dto.Value == "Intermediate");
        result.Should().ContainSingle(dto => dto.Value == "Advanced");
    }

    // ── AssignSubstagePlayMode ────────────────────────────────────────────────

    [Fact]
    public async Task AssignSubstagePlayMode_ChangesPlayModeFromTreasureHuntToTrivia()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        repository.Seed(mission);
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Substage", 1));
        substage.Id = 20;

        var handler = new AssignSubstagePlayModeCommandHandler(repository);

        var result = await handler.Handle(
            new AssignSubstagePlayModeCommand(mission.Id, stage.Id, substage.Id, "Trivia"),
            CancellationToken.None);

        result.Stages!.Single().Substages!.Single().PlayMode.Should().Be("Trivia");
    }

    [Fact]
    public async Task AssignSubstagePlayMode_WhenSameMode_ReturnsUnchangedMission()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        repository.Seed(mission);
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Substage", 1));
        substage.Id = 20;

        var handler = new AssignSubstagePlayModeCommandHandler(repository);

        var result = await handler.Handle(
            new AssignSubstagePlayModeCommand(mission.Id, stage.Id, substage.Id, "TreasureHunt"),
            CancellationToken.None);

        result.Stages!.Single().Substages!.Single().PlayMode.Should().Be("TreasureHunt");
    }

    // ── RemoveMissionNode ─────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveMissionNode_RemovesStageFromMission()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        repository.Seed(mission);
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 10;

        var handler = new RemoveMissionNodeCommandHandler(repository);

        var result = await handler.Handle(
            new RemoveMissionNodeCommand(mission.Id, stage.Id),
            CancellationToken.None);

        result.Stages.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveMissionNode_RemovesSubstageFromStage()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        repository.Seed(mission);
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Substage", 1));
        substage.Id = 20;

        var handler = new RemoveMissionNodeCommandHandler(repository);

        var result = await handler.Handle(
            new RemoveMissionNodeCommand(mission.Id, substage.Id),
            CancellationToken.None);

        result.Stages!.Single().Substages.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveMissionNode_RemovesClueFromSubstage()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        repository.Seed(mission);
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Substage", 1));
        substage.Id = 20;
        var clue = mission.AddClue(stage.Id, substage.Id, Clue.Create("Clue", 1, "Text"));
        clue.Id = 30;

        var handler = new RemoveMissionNodeCommandHandler(repository);

        var result = await handler.Handle(
            new RemoveMissionNodeCommand(mission.Id, clue.Id),
            CancellationToken.None);

        result.Stages!.Single().Substages!.Single().Clues.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveMissionNode_WhenNodeNotFound_ThrowsNotFoundException()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        repository.Seed(mission);

        var handler = new RemoveMissionNodeCommandHandler(repository);

        var act = () => handler.Handle(new RemoveMissionNodeCommand(mission.Id, 999), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── RemoveTarget ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveTarget_RemovesTargetFromSubstage()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        repository.Seed(mission);
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Substage", 1));
        substage.Id = 20;
        var target = mission.AddTarget(stage.Id, substage.Id, "Target", "QR-1", 1);
        target.Id = 30;

        var handler = new RemoveTargetCommandHandler(repository);

        var result = await handler.Handle(
            new RemoveTargetCommand(mission.Id, stage.Id, substage.Id, target.Id),
            CancellationToken.None);

        result.Stages!.Single().Substages!.Single().Targets.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveTarget_WhenTargetNotFound_ThrowsNotFoundException()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        repository.Seed(mission);
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Substage", 1));
        substage.Id = 20;

        var handler = new RemoveTargetCommandHandler(repository);

        var act = () => handler.Handle(
            new RemoveTargetCommand(mission.Id, stage.Id, substage.Id, 999),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── UpdateMissionNode ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateMissionNode_RenamesStage()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        repository.Seed(mission);
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 10;

        var handler = new UpdateMissionNodeCommandHandler(repository);

        var result = await handler.Handle(
            new UpdateMissionNodeCommand(mission.Id, stage.Id, "Renamed Stage", 2),
            CancellationToken.None);

        result.Stages!.Single().Title.Should().Be("Renamed Stage");
        result.Stages!.Single().SequenceOrder.Should().Be(2);
    }

    [Fact]
    public async Task UpdateMissionNode_RenamesSubstage()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        repository.Seed(mission);
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Substage", 1));
        substage.Id = 20;

        var handler = new UpdateMissionNodeCommandHandler(repository);

        var result = await handler.Handle(
            new UpdateMissionNodeCommand(mission.Id, substage.Id, "Renamed Substage", 2),
            CancellationToken.None);

        result.Stages!.Single().Substages!.Single().Title.Should().Be("Renamed Substage");
    }

    [Fact]
    public async Task UpdateMissionNode_RenamesClueAndUpdatesText()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        repository.Seed(mission);
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Substage", 1));
        substage.Id = 20;
        var clue = mission.AddClue(stage.Id, substage.Id, Clue.Create("Original Clue", 1, "Old text"));
        clue.Id = 30;

        var handler = new UpdateMissionNodeCommandHandler(repository);

        var result = await handler.Handle(
            new UpdateMissionNodeCommand(mission.Id, clue.Id, "Updated Clue", 1, "New text"),
            CancellationToken.None);

        var updatedClue = result.Stages!.Single().Substages!.Single().Clues!.Single();
        updatedClue.Title.Should().Be("Updated Clue");
        updatedClue.Text.Should().Be("New text");
    }

    [Fact]
    public async Task UpdateMissionNode_WhenNodeNotFound_ThrowsNotFoundException()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        repository.Seed(mission);

        var handler = new UpdateMissionNodeCommandHandler(repository);

        var act = () => handler.Handle(
            new UpdateMissionNodeCommand(mission.Id, 999, "Title", 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── UpdateTarget ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTarget_UpdatesTargetFieldsScoreAndWinnerScore()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        repository.Seed(mission);
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Substage", 1));
        substage.Id = 20;
        var target = mission.AddTarget(stage.Id, substage.Id, "Target", "QR-1", 1);
        target.Id = 30;

        var handler = new UpdateTargetCommandHandler(repository);

        var result = await handler.Handle(
            new UpdateTargetCommand(mission.Id, stage.Id, substage.Id, target.Id, "Updated Target", "QR-2", 2, false, 50, 50),
            CancellationToken.None);

        var updatedTarget = result.Stages!.Single().Substages!.Single().Targets!.Single();
        updatedTarget.Name.Should().Be("Updated Target");
        updatedTarget.QrCode.Should().Be("QR-2");
        updatedTarget.Score.Should().Be(50);
        result.Stages!.Single().Substages!.Single().WinnerScore.Should().Be(50);
    }

    [Fact]
    public async Task UpdateTarget_WithoutWinnerScore_UpdatesOnlyTargetFields()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        repository.Seed(mission);
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Substage", 1));
        substage.Id = 20;
        var target = mission.AddTarget(stage.Id, substage.Id, "Target", "QR-1", 1);
        target.Id = 30;

        var handler = new UpdateTargetCommandHandler(repository);

        var result = await handler.Handle(
            new UpdateTargetCommand(mission.Id, stage.Id, substage.Id, target.Id, "Renamed", "QR-1", 1, true),
            CancellationToken.None);

        result.Stages!.Single().Substages!.Single().Targets!.Single().Name.Should().Be("Renamed");
    }

    // ── UpdateTriviaQuizSelection ─────────────────────────────────────────────

    [Fact]
    public async Task UpdateTriviaQuizSelection_WhenQuizIsPublished_ReplacesSelection()
    {
        var missionRepository = new InMemoryMissionRepository();
        var quizRepository = new InMemoryTriviaQuizRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        missionRepository.Seed(mission);
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTrivia("Trivia", 1));
        substage.Id = 20;

        var firstQuiz = CreatePublishedTriviaQuiz();
        quizRepository.Seed(firstQuiz);
        mission.SelectTriviaQuiz(stage.Id, substage.Id, firstQuiz.Id);

        var newQuiz = CreatePublishedTriviaQuiz();
        quizRepository.Seed(newQuiz);

        var handler = new SelectTriviaQuizCommandHandler(missionRepository, quizRepository);

        var result = await handler.Handle(
            new SelectTriviaQuizCommand(mission.Id, stage.Id, substage.Id, newQuiz.Id),
            CancellationToken.None);

        result.Stages!.Single().Substages!.Single().TriviaQuizSelection!.TriviaQuizId.Should().Be(newQuiz.Id);
    }

    [Fact]
    public async Task UpdateTriviaQuizSelection_WhenQuizIsNotPublished_ThrowsValidationException()
    {
        var missionRepository = new InMemoryMissionRepository();
        var quizRepository = new InMemoryTriviaQuizRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        missionRepository.Seed(mission);
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTrivia("Trivia", 1));
        substage.Id = 20;

        var draftQuiz = TriviaQuiz.Create("Draft", "Description", [CreateTriviaQuestion()]);
        quizRepository.Seed(draftQuiz);

        var handler = new SelectTriviaQuizCommandHandler(missionRepository, quizRepository);

        var act = () => handler.Handle(
            new SelectTriviaQuizCommand(mission.Id, stage.Id, substage.Id, draftQuiz.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static TriviaQuiz CreatePublishedTriviaQuiz()
    {
        var quiz = TriviaQuiz.Create("Quiz", "Description", [CreateTriviaQuestion()]);
        quiz.Publish(DateTimeOffset.UtcNow);
        return quiz;
    }

    private static TriviaQuestion CreateTriviaQuestion()
    {
        return TriviaQuestion.Create(
            "Question?",
            1,
            10,
            30,
            null,
            [
                TriviaOption.Create("A", 1, true),
                TriviaOption.Create("B", 2, false)
            ]);
    }
}
