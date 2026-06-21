using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.SessionOperations.UnitTests.Domain.ValueObjects;

public sealed class StageSnapshotTests
{
    [Fact]
    public void Create_WithoutSubstages_ThrowsException()
    {
        var act = () => StageSnapshot.Create("Stage One", 1, []);

        act.Should().Throw<StageSnapshotMustContainSubstagesException>();
    }

    [Fact]
    public void Create_WithOutOfOrderSubstages_ThrowsException()
    {
        var act = () => StageSnapshot.Create(
            "Stage One",
            1,
            [
                SubstageSnapshot.CreateTreasureHunt("Treasure Route", 2, winnerScore: 100),
                SubstageSnapshot.CreateTrivia("Trivia Round", 3)
            ]);

        act.Should().Throw<MissionRuntimeSnapshotSubstageOrderInvalidException>();
    }
}
