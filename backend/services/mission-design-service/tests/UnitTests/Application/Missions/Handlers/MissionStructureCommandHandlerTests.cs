using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Missions.Commands.ActivateMission;
using umbral_backend.Application.Missions.Commands.AddMissionNode;
using umbral_backend.Application.Missions.Commands.AddTarget;
using umbral_backend.Application.Missions.Commands.AssociateClueWithTarget;
using umbral_backend.Application.Missions.Commands.SelectTriviaQuiz;
using umbral_backend.Application.Missions.Commands.UnassociateClueFromTarget;
using umbral_backend.Application.Missions.Commands.AssignSubstagePlayMode;
using umbral_backend.Application.Missions.Commands.CreateMission;
using umbral_backend.Application.Missions.Commands.DeactivateMission;
using umbral_backend.Application.Missions.Commands.RemoveMissionNode;
using umbral_backend.Application.Missions.Commands.RemoveTarget;
using umbral_backend.Application.Missions.Commands.UpdateMission;
using umbral_backend.Application.Missions.Commands.UpdateMissionNode;
using umbral_backend.Application.Missions.Commands.UpdateTarget;
using umbral_backend.Application.Missions.Queries.GetDifficultyCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionDetail;
using umbral_backend.Application.Missions.Queries.GetMissionReadiness;
using umbral_backend.Application.Missions.Queries.GetMissionRuntimePlan;
using umbral_backend.Application.UnitTests.Application.Missions.TestDoubles;
using umbral_backend.Application.UnitTests.Application.Trivias.TestDoubles;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Application.Missions.Handlers;

public sealed class MissionStructureCommandHandlerTests
{
    [Fact]
    public async Task AddMissionNode_AddsStageSubstageAndClueIntoCompositeShape()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 30);
        repository.Seed(mission);

        var handler = new AddMissionNodeCommandHandler(repository);

        var stageResult = await handler.Handle(
            new AddMissionNodeCommand(mission.Id, "Stage", "Stage 1", 1),
            CancellationToken.None);
        var stage = mission.Stages.Single();
        stage.Id = 10;

        var substageResult = await handler.Handle(
            new AddMissionNodeCommand(mission.Id, "Substage", "Substage 1", 1, stage.Id, PlayMode: "TreasureHunt"),
            CancellationToken.None);
        var substage = stage.Substages.Single();
        substage.Id = 20;

        var clueResult = await handler.Handle(
            new AddMissionNodeCommand(
                mission.Id,
                "Clue",
                "Clue 1",
                1,
                stage.Id,
                substage.Id,
                ClueText: "Look near the archive."),
            CancellationToken.None);

        stageResult.Stages!.Should().ContainSingle().Which.Title.Should().Be("Stage 1");
        substageResult.Stages!.Single().Substages!.Should().ContainSingle().Which.PlayMode.Should().Be("TreasureHunt");
        clueResult.Stages!.Single().Substages!.Single().Clues!.Should().ContainSingle().Which.Text.Should().Be("Look near the archive.");
        repository.LastUpdatedMission.Should().BeSameAs(mission);
    }

    [Fact]
    public async Task AddTarget_AddsTargetWithDifficultyDerivedScore()
    {
        var repository = new InMemoryMissionRepository();
        var mission = CreateMissionWithTreasureSubstage(repository, out var stage, out var substage);
        var handler = new AddTargetCommandHandler(repository);

        var result = await handler.Handle(
            new AddTargetCommand(mission.Id, stage.Id, substage.Id, "Target A", "QR-A", 1, 4.711, -74.0721),
            CancellationToken.None);

        // Score is derived from the mission's difficulty (Advanced => 50 * 3).
        var resultSubstage = result.Stages!.Single().Substages!.Single();
        resultSubstage.Targets!.Should().ContainSingle(target => target.Name == "Target A" && target.QrCode == "QR-A" && target.Score == 150);
    }

    [Fact]
    public async Task AssociateAndUnassociateClue_EnforcesOneOptionalCluePerTarget()
    {
        var repository = new InMemoryMissionRepository();
        var mission = CreateMissionWithTreasureSubstage(repository, out var stage, out var substage);
        var clue = mission.AddClue(stage.Id, substage.Id, Clue.Create("Clue", 1, "Look up"));
        clue.Id = 40;
        var target = mission.AddTarget(stage.Id, substage.Id, "Target", "QR", 1, 4.711, -74.0721);
        target.Id = 50;

        var associateHandler = new AssociateClueWithTargetCommandHandler(repository);
        var associateResult = await associateHandler.Handle(
            new AssociateClueWithTargetCommand(mission.Id, stage.Id, substage.Id, target.Id, clue.Id),
            CancellationToken.None);

        associateResult.Stages!.Single().Substages!.Single().Targets!.Single().ClueId.Should().Be(clue.Id);

        var unassociateHandler = new UnassociateClueFromTargetCommandHandler(repository);
        var unassociateResult = await unassociateHandler.Handle(
            new UnassociateClueFromTargetCommand(mission.Id, stage.Id, substage.Id, target.Id),
            CancellationToken.None);

        unassociateResult.Stages!.Single().Substages!.Single().Targets!.Single().ClueId.Should().BeNull();
    }

    [Fact]
    public async Task SetTriviaQuizSelection_WhenQuizIsPublished_SelectsQuizForTriviaSubstage()
    {
        var missionRepository = new InMemoryMissionRepository();
        var quizRepository = new InMemoryTriviaQuizRepository();
        var mission = CreateMissionWithTriviaSubstage(missionRepository, out var stage, out var substage);
        var quiz = CreatePublishedTriviaQuiz();
        quizRepository.Seed(quiz);

        var handler = new SelectTriviaQuizCommandHandler(missionRepository, quizRepository);

        var result = await handler.Handle(
            new SelectTriviaQuizCommand(mission.Id, stage.Id, substage.Id, quiz.Id),
            CancellationToken.None);

        var selection = result.Stages!.Single().Substages!.Single().TriviaQuizSelection;
        selection.Should().NotBeNull();
        selection!.TriviaQuizId.Should().Be(quiz.Id);
    }

    [Fact]
    public async Task SetTriviaQuizSelection_WhenQuizIsNotPublished_ThrowsValidationException()
    {
        var missionRepository = new InMemoryMissionRepository();
        var quizRepository = new InMemoryTriviaQuizRepository();
        var mission = CreateMissionWithTriviaSubstage(missionRepository, out var stage, out var substage);
        var quiz = TriviaQuiz.Create("Draft quiz", "Draft description", [CreateTriviaQuestion()]);
        quizRepository.Seed(quiz);
        var handler = new SelectTriviaQuizCommandHandler(missionRepository, quizRepository);

        var act = () => handler.Handle(
            new SelectTriviaQuizCommand(mission.Id, stage.Id, substage.Id, quiz.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ActivateMission_WhenRuntimePlanIsReady_ActivatesThroughDomainPolicy()
    {
        var repository = new InMemoryMissionRepository();
        var mission = CreateMissionWithTreasureSubstage(repository, out var stage, out var substage);
        mission.AddTarget(stage.Id, substage.Id, "Target", "QR", 1, 4.711, -74.0721);

        var handler = new ActivateMissionCommandHandler(repository, new InMemoryTriviaQuizRepository());

        var result = await handler.Handle(new ActivateMissionCommand(mission.Id), CancellationToken.None);

        result.Status.Should().Be("Ready");
    }

    [Fact]
    public async Task ActivateMission_WhenRuntimePlanIsNotReady_ThrowsDomainReadinessException()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 30);
        repository.Seed(mission);
        var handler = new ActivateMissionCommandHandler(repository, new InMemoryTriviaQuizRepository());

        var act = () => handler.Handle(new ActivateMissionCommand(mission.Id), CancellationToken.None);

        await act.Should().ThrowAsync<MissionNotReadyForActivationException>();
    }

    [Fact]
    public async Task GetMissionReadiness_WhenMissionDoesNotExist_ThrowsNotFound()
    {
        var handler = new GetMissionReadinessQueryHandler(
            new InMemoryMissionRepository(), new InMemoryTriviaQuizRepository());

        var act = () => handler.Handle(new GetMissionReadinessQuery(99), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Entity \"Mission\" (99) was not found.");
    }

    [Fact]
    public async Task GetMissionReadiness_ReturnsDomainPolicyFailures()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 30);
        repository.Seed(mission);
        var handler = new GetMissionReadinessQueryHandler(repository, new InMemoryTriviaQuizRepository());

        var result = await handler.Handle(new GetMissionReadinessQuery(mission.Id), CancellationToken.None);

        result.IsReady.Should().BeFalse();
        result.Failures!.Should().Contain("Mission must contain at least one stage.");
    }

    [Fact]
    public async Task ActivateMission_WhenSelectedQuizArchived_ThrowsReadinessException()
    {
        var missionRepository = new InMemoryMissionRepository();
        var quizRepository = new InMemoryTriviaQuizRepository();
        var mission = CreateMissionWithTriviaSubstage(missionRepository, out var stage, out var substage);
        var quiz = CreatePublishedTriviaQuiz();
        quizRepository.Seed(quiz);
        mission.SelectTriviaQuiz(stage.Id, substage.Id, quiz.Id);
        quiz.Archive(DateTimeOffset.UtcNow);
        var handler = new ActivateMissionCommandHandler(missionRepository, quizRepository);

        var act = () => handler.Handle(new ActivateMissionCommand(mission.Id), CancellationToken.None);

        await act.Should().ThrowAsync<MissionNotReadyForActivationException>()
            .WithMessage("*must select a published trivia quiz*");
    }

    [Fact]
    public async Task ActivateMission_WhenSelectedQuizPublished_Activates()
    {
        var missionRepository = new InMemoryMissionRepository();
        var quizRepository = new InMemoryTriviaQuizRepository();
        var mission = CreateMissionWithTriviaSubstage(missionRepository, out var stage, out var substage);
        var quiz = CreatePublishedTriviaQuiz();
        quizRepository.Seed(quiz);
        mission.SelectTriviaQuiz(stage.Id, substage.Id, quiz.Id);
        var handler = new ActivateMissionCommandHandler(missionRepository, quizRepository);

        var result = await handler.Handle(new ActivateMissionCommand(mission.Id), CancellationToken.None);

        result.Status.Should().Be("Ready");
    }

    [Fact]
    public async Task GetMissionReadiness_WhenSelectedQuizArchived_IsNotReadyWithPublicationFailure()
    {
        var missionRepository = new InMemoryMissionRepository();
        var quizRepository = new InMemoryTriviaQuizRepository();
        var mission = CreateMissionWithTriviaSubstage(missionRepository, out var stage, out var substage);
        var quiz = CreatePublishedTriviaQuiz();
        quizRepository.Seed(quiz);
        mission.SelectTriviaQuiz(stage.Id, substage.Id, quiz.Id);
        quiz.Archive(DateTimeOffset.UtcNow);
        var handler = new GetMissionReadinessQueryHandler(missionRepository, quizRepository);

        var result = await handler.Handle(new GetMissionReadinessQuery(mission.Id), CancellationToken.None);

        result.IsReady.Should().BeFalse();
        result.Failures!.Should().Contain(failure => failure.Contains("must select a published trivia quiz"));
    }

    [Fact]
    public async Task GetMissionReadiness_WhenSelectedQuizPublished_IsReady()
    {
        var missionRepository = new InMemoryMissionRepository();
        var quizRepository = new InMemoryTriviaQuizRepository();
        var mission = CreateMissionWithTriviaSubstage(missionRepository, out var stage, out var substage);
        var quiz = CreatePublishedTriviaQuiz();
        quizRepository.Seed(quiz);
        mission.SelectTriviaQuiz(stage.Id, substage.Id, quiz.Id);
        var handler = new GetMissionReadinessQueryHandler(missionRepository, quizRepository);

        var result = await handler.Handle(new GetMissionReadinessQuery(mission.Id), CancellationToken.None);

        result.IsReady.Should().BeTrue();
        result.Failures.Should().BeEmpty();
    }

    [Fact]
    public async Task ActivateMission_WhenTriviaTimersExceedMaximumTime_ThrowsReadinessException()
    {
        var missionRepository = new InMemoryMissionRepository();
        var quizRepository = new InMemoryTriviaQuizRepository();
        // 1-minute mission = 60s budget; quiz of 3 × 30s = 90s cannot fit.
        var mission = CreateShortMissionWithTriviaSubstage(missionRepository, out var stage, out var substage);
        var quiz = CreatePublishedTriviaQuiz(questionCount: 3, timerSeconds: 30);
        quizRepository.Seed(quiz);
        mission.SelectTriviaQuiz(stage.Id, substage.Id, quiz.Id);
        var handler = new ActivateMissionCommandHandler(missionRepository, quizRepository);

        var act = () => handler.Handle(new ActivateMissionCommand(mission.Id), CancellationToken.None);

        await act.Should().ThrowAsync<MissionNotReadyForActivationException>()
            .WithMessage("*exceeds the mission maximum time*");
    }

    [Fact]
    public async Task GetMissionReadiness_WhenTriviaTimersExceedMaximumTime_IsNotReadyWithTimeBudgetFailure()
    {
        var missionRepository = new InMemoryMissionRepository();
        var quizRepository = new InMemoryTriviaQuizRepository();
        var mission = CreateShortMissionWithTriviaSubstage(missionRepository, out var stage, out var substage);
        var quiz = CreatePublishedTriviaQuiz(questionCount: 3, timerSeconds: 30);
        quizRepository.Seed(quiz);
        mission.SelectTriviaQuiz(stage.Id, substage.Id, quiz.Id);
        var handler = new GetMissionReadinessQueryHandler(missionRepository, quizRepository);

        var result = await handler.Handle(new GetMissionReadinessQuery(mission.Id), CancellationToken.None);

        result.IsReady.Should().BeFalse();
        result.Failures!.Should().Contain(failure => failure.Contains("exceeds the mission maximum time"));
    }

    [Fact]
    public async Task GetMissionReadiness_WhenTriviaTimersEqualMaximumTime_IsReady()
    {
        var missionRepository = new InMemoryMissionRepository();
        var quizRepository = new InMemoryTriviaQuizRepository();
        // 1-minute mission = 60s budget; quiz of 2 × 30s = 60s sits exactly at the boundary.
        var mission = CreateShortMissionWithTriviaSubstage(missionRepository, out var stage, out var substage);
        var quiz = CreatePublishedTriviaQuiz(questionCount: 2, timerSeconds: 30);
        quizRepository.Seed(quiz);
        mission.SelectTriviaQuiz(stage.Id, substage.Id, quiz.Id);
        var handler = new GetMissionReadinessQueryHandler(missionRepository, quizRepository);

        var result = await handler.Handle(new GetMissionReadinessQuery(mission.Id), CancellationToken.None);

        result.IsReady.Should().BeTrue();
        result.Failures.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveMissionNode_OnDeactivatedMission_IsRejected()
    {
        var repository = new InMemoryMissionRepository();
        var mission = CreateMissionWithTreasureSubstage(repository, out _, out var substage);
        mission.Deactivate(DateTimeOffset.UtcNow);

        var handler = new RemoveMissionNodeCommandHandler(repository);

        // The substage branch mutates the child node directly (bypassing an aggregate mutator);
        // HU-09's terminal-retirement guard must still reject it.
        var act = () => handler.Handle(
            new RemoveMissionNodeCommand(mission.Id, substage.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<MissionNotEditableWhileInactiveException>();
    }

    [Fact]
    public async Task UpdateMissionNode_RenamingSubstageOnDeactivatedMission_IsRejected()
    {
        var repository = new InMemoryMissionRepository();
        var mission = CreateMissionWithTreasureSubstage(repository, out _, out var substage);
        mission.Deactivate(DateTimeOffset.UtcNow);

        var handler = new UpdateMissionNodeCommandHandler(repository);

        // Renaming a substage mutates the child directly; the guard must reject it on a retired mission.
        var act = () => handler.Handle(
            new UpdateMissionNodeCommand(mission.Id, substage.Id, "Renamed", 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<MissionNotEditableWhileInactiveException>();
    }

    private static Mission CreateMissionWithTreasureSubstage(
        InMemoryMissionRepository repository,
        out Stage stage,
        out Substage substage)
    {
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 30);
        repository.Seed(mission);
        stage = mission.AddStage("Stage", 1);
        stage.Id = 10;
        substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Substage", 1));
        substage.Id = 20;
        return mission;
    }

    private static Mission CreateMissionWithTriviaSubstage(
        InMemoryMissionRepository repository,
        out Stage stage,
        out Substage substage)
    {
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 30);
        repository.Seed(mission);
        stage = mission.AddStage("Stage", 1);
        stage.Id = 10;
        substage = mission.AddSubstage(stage.Id, Substage.CreateTrivia("Trivia", 1));
        substage.Id = 20;
        return mission;
    }

    private static Mission CreateShortMissionWithTriviaSubstage(
        InMemoryMissionRepository repository,
        out Stage stage,
        out Substage substage)
    {
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 1); // 1 minute = 60s budget
        repository.Seed(mission);
        stage = mission.AddStage("Stage", 1);
        stage.Id = 10;
        substage = mission.AddSubstage(stage.Id, Substage.CreateTrivia("Trivia", 1));
        substage.Id = 20;
        return mission;
    }

    private static TriviaQuiz CreatePublishedTriviaQuiz()
    {
        var quiz = TriviaQuiz.Create("Quiz", "Description", [CreateTriviaQuestion()]);
        quiz.Publish(DateTimeOffset.UtcNow);
        return quiz;
    }

    private static TriviaQuiz CreatePublishedTriviaQuiz(int questionCount, int timerSeconds)
    {
        var questions = Enumerable.Range(0, questionCount)
            .Select(index => TriviaQuestion.Create(
                $"Question {index}?",
                10,
                timerSeconds,
                null,
                [
                    TriviaOption.Create("A", 1, true),
                    TriviaOption.Create("B", 2, false)
                ]))
            .ToList();

        var quiz = TriviaQuiz.Create("Quiz", "Description", questions);
        quiz.Publish(DateTimeOffset.UtcNow);
        return quiz;
    }

    private static TriviaQuestion CreateTriviaQuestion()
    {
        return TriviaQuestion.Create(
            "Question?",
            10,
            30,
            null,
            [
                TriviaOption.Create("A", 1, true),
                TriviaOption.Create("B", 2, false)
            ]);
    }
}
