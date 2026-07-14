using umbral_backend.Domain.Enums;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Enums;

public sealed class ReleaseModeTests
{
    [Fact]
    public void Values_AreStable()
    {
        ((int)ReleaseMode.Manual).Should().Be(1);
        ((int)ReleaseMode.Automatic).Should().Be(2);
        ((int)ReleaseMode.Policy).Should().Be(3);
    }
}
