using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Domain.Entities;

public class MissionNodeTests
{
    [Fact]
    public void Stage_AllowsSubstageChild()
    {
        var stage = Stage.Create("Stage", 1);

        var substage = stage.AddSubstage(Substage.CreateTrivia("Sub", 1));

        stage.NodeType.Should().Be(MissionNodeType.Stage);
        stage.Children.Should().ContainSingle().Which.Should().BeSameAs(substage);
    }

    [Fact]
    public void Stage_RejectsClueChild()
    {
        var stage = Stage.Create("Stage", 1);

        var act = () => stage.AddChild(Clue.Create("Clue", 1, "text"));

        act.Should().Throw<InvalidMissionNodeChildException>()
            .Which.Child.Should().Be(MissionNodeType.Clue);
    }

    [Fact]
    public void Stage_RejectsNestedStageChild()
    {
        var stage = Stage.Create("Stage", 1);

        var act = () => stage.AddChild(Stage.Create("Nested", 1));

        act.Should().Throw<InvalidMissionNodeChildException>();
    }

    [Fact]
    public void Substage_AllowsClueChild()
    {
        var substage = Substage.CreateTreasureHunt("Sub", 1);

        var clue = substage.AddClue(Clue.Create("Clue", 1, "text"));

        substage.NodeType.Should().Be(MissionNodeType.Substage);
        substage.Clues.Should().ContainSingle().Which.Should().BeSameAs(clue);
    }

    [Fact]
    public void Substage_RejectsSubstageChild()
    {
        var substage = Substage.CreateTrivia("Sub", 1);

        var act = () => substage.AddChild(Substage.CreateTrivia("Nested", 1));

        act.Should().Throw<InvalidMissionNodeChildException>();
    }

    [Fact]
    public void Clue_IsLeafAndRejectsAnyChild()
    {
        var clue = Clue.Create("Clue", 1, "text");

        var act = () => clue.AddChild(Clue.Create("Nested", 1, "text"));

        clue.NodeType.Should().Be(MissionNodeType.Clue);
        act.Should().Throw<InvalidMissionNodeChildException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WhenTitleBlank_Throws(string? title)
    {
        var act = () => Stage.Create(title!, 1);

        act.Should().Throw<MissionNodeTitleRequiredException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WhenSequenceOrderNotPositive_Throws(int sequenceOrder)
    {
        var act = () => Stage.Create("Stage", sequenceOrder);

        act.Should().Throw<MissionNodeSequenceOrderMustBePositiveException>();
    }

    [Fact]
    public void Children_AreOrderedBySequenceOrder()
    {
        var stage = Stage.Create("Stage", 1);
        stage.AddSubstage(Substage.CreateTrivia("Second", 2));
        stage.AddSubstage(Substage.CreateTrivia("First", 1));

        stage.Children.Select(c => c.Title).Should().ContainInOrder("First", "Second");
    }

    [Fact]
    public void Descendants_WalksTheWholeTree()
    {
        var stage = Stage.Create("Stage", 1);
        var substage = Substage.CreateTreasureHunt("Sub", 1);
        stage.AddSubstage(substage);
        substage.AddClue(Clue.Create("Clue", 1, "text"));

        stage.Descendants().Select(n => n.NodeType)
            .Should().ContainInOrder(MissionNodeType.Substage, MissionNodeType.Clue);
    }

    [Fact]
    public void Clue_DefaultsToHiddenUntilOperatorRelease()
    {
        var clue = Clue.Create("Clue", 1, "text");

        clue.Visibility.Should().Be(ClueVisibilityPolicy.HiddenUntilOperatorRelease);
    }
}
