using umbral_backend.Domain.Enums;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Enums;

public sealed class SessionModeTests
{
    [Fact]
    public void TreasureHunt_HasStableValue()
    {
        ((int)SessionMode.TreasureHunt).Should().Be(1);
    }
}
