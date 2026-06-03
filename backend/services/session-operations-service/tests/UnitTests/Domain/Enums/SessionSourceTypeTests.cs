using umbral_backend.Domain.Enums;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Enums;

public sealed class SessionSourceTypeTests
{
    [Fact]
    public void Mission_HasStableValue()
    {
        ((int)SessionSourceType.Mission).Should().Be(1);
    }
}
