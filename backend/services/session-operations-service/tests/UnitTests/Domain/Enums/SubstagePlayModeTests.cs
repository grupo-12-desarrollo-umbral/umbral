using umbral_backend.Domain.Enums;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Enums;

public sealed class SubstagePlayModeTests
{
    [Fact]
    public void TreasureHunt_HasStableValue()
    {
        ((int)SubstagePlayMode.TreasureHunt).Should().Be(1);
    }

    [Fact]
    public void Trivia_HasStableValue()
    {
        ((int)SubstagePlayMode.Trivia).Should().Be(2);
    }
}
