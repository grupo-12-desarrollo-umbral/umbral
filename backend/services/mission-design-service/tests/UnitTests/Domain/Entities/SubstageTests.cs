using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Domain.Entities;

public class SubstageTests
{
    [Fact]
    public void CreateTreasureHunt_HasTreasureHuntPlayMode()
    {
        var substage = Substage.CreateTreasureHunt("Sub", 1);

        substage.PlayMode.Should().Be(SubstagePlayMode.TreasureHunt);
    }

    [Fact]
    public void CreateTrivia_HasTriviaPlayMode()
    {
        var substage = Substage.CreateTrivia("Sub", 1);

        substage.PlayMode.Should().Be(SubstagePlayMode.Trivia);
    }

    [Fact]
    public void AddTarget_OrdersTargetsBySequence()
    {
        var substage = Substage.CreateTreasureHunt("Sub", 1);
        substage.AddTarget("Second", "QR-2", 2, 30, 4.711, -74.0721);
        substage.AddTarget("First", "QR-1", 1, 20, 4.711, -74.0721);

        substage.Targets.Select(t => t.Name).Should().ContainInOrder("First", "Second");
    }

    [Fact]
    public void SelectTriviaQuiz_OnTreasureHunt_Throws()
    {
        var substage = Substage.CreateTreasureHunt("Sub", 1);

        var act = () => substage.SelectTriviaQuiz(42);

        act.Should().Throw<SubstagePlayModeMismatchException>();
    }

    [Fact]
    public void SelectTriviaQuiz_StoresQuizId()
    {
        var substage = Substage.CreateTrivia("Sub", 1);

        substage.SelectTriviaQuiz(42);

        substage.TriviaQuizId.Should().Be(42);
    }

    [Fact]
    public void AssociateClueWithTarget_WhenTargetAlreadyHasDifferentClue_Throws()
    {
        var substage = Substage.CreateTreasureHunt("Sub", 1);
        var target = substage.AddTarget("Statue", "QR-1", 1, 20, 4.711, -74.0721);
        target.Id = 9;

        var firstClue = Clue.Create("Hint A", 1, "text");
        firstClue.Id = 1;
        var secondClue = Clue.Create("Hint B", 2, "text");
        secondClue.Id = 2;
        substage.AddClue(firstClue);
        substage.AddClue(secondClue);

        substage.AssociateClueWithTarget(9, firstClue);
        var act = () => substage.AssociateClueWithTarget(9, secondClue);

        act.Should().Throw<TargetMayReferenceAtMostOneClueException>();
    }

    [Fact]
    public void RemoveTarget_WhenMissing_Throws()
    {
        var substage = Substage.CreateTreasureHunt("Sub", 1);

        var act = () => substage.RemoveTarget(404);

        act.Should().Throw<TargetNotFoundException>();
    }
}
