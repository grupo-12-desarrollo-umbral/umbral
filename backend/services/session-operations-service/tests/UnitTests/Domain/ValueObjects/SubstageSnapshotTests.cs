using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.SessionOperations.UnitTests.Domain.ValueObjects;

public sealed class SubstageSnapshotTests
{
    [Fact]
    public void CreateTreasureHunt_PreservesWinnerScore()
    {
        var snapshot = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1, winnerScore: 150);

        snapshot.WinnerScore.Should().Be(150);
    }

    [Fact]
    public void TriviaCtor_WithWinnerScore_ThrowsException()
    {
        var ctor = typeof(SubstageSnapshot)
            .GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters().Length == 5);

        var act = () => ctor.Invoke(
            [
                Guid.NewGuid(),
                "Trivia Round",
                1,
                umbral_backend.Domain.Enums.SubstagePlayMode.Trivia,
                100
            ]);

        act.Should().Throw<System.Reflection.TargetInvocationException>()
            .WithInnerException<TriviaSubstageSnapshotCannotDeclareWinnerScoreException>();
    }
}
