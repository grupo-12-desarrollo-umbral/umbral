using umbral_backend.Domain.Enums;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.SessionOperations.UnitTests.Domain.ValueObjects;

public sealed class SubstageSnapshotTests
{
    [Fact]
    public void CreateTreasureHunt_SetsTreasureHuntPlayMode()
    {
        var snapshot = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1);

        snapshot.PlayMode.Should().Be(SubstagePlayMode.TreasureHunt);
        snapshot.SequenceOrder.Should().Be(1);
    }

    [Fact]
    public void CreateTrivia_SetsTriviaPlayMode()
    {
        var snapshot = SubstageSnapshot.CreateTrivia("Trivia Round", 2);

        snapshot.PlayMode.Should().Be(SubstagePlayMode.Trivia);
        snapshot.SequenceOrder.Should().Be(2);
    }
}
