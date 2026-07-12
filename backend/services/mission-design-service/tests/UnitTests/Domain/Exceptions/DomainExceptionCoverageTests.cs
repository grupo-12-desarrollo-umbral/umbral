using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Domain.Exceptions;

public sealed class DomainExceptionCoverageTests
{
    [Fact]
    public void InvalidDifficultyValue_ContainsRejectedValue()
    {
        var act = () => Difficulty.Create("SuperHard");

        act.Should().Throw<InvalidDifficultyValueException>()
            .WithMessage("*SuperHard*");
    }

    [Fact]
    public void MissionAlreadyActive_ThrownWhenActivatingReadyMission()
    {
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        var stage = mission.AddStage("Stage", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Substage", 1));
        substage.Id = 20;
        mission.AddTarget(stage.Id, substage.Id, "Target", "QR", 1, 4.711, -74.0721);
        mission.Activate();

        var act = () => mission.Activate();

        act.Should().Throw<MissionAlreadyActiveException>()
            .WithMessage("Mission is already active.");
    }

    [Fact]
    public void SubstageRequiresPlayMode_ThrownWhenSelectingTriviaQuizWithInvalidId()
    {
        var substage = Substage.CreateTrivia("Trivia", 1);

        var act = () => substage.SelectTriviaQuiz(0);

        act.Should().Throw<SubstageRequiresPlayModeException>()
            .WithMessage("A substage must declare exactly one play mode.");
    }

    [Fact]
    public void TriviaOptionSequenceOrderMustBeUnique_ThrownForDuplicateOptionOrders()
    {
        var quiz = TriviaQuiz.Create("Quiz", "Description");

        var act = () => quiz.AddQuestion(
            "Question?",
            10,
            30,
            null,
            [TriviaOption.Create("A", 1, true), TriviaOption.Create("B", 1, false)]);

        act.Should().Throw<TriviaOptionSequenceOrderMustBeUniqueException>()
            .WithMessage("*unique*");
    }

}
