using umbral_backend.Domain.Enums;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Enums;

public sealed class SessionEventActorTypeTests
{
    [Fact]
    public void Values_AreStable()
    {
        ((int)SessionEventActorType.Operator).Should().Be(1);
        ((int)SessionEventActorType.System).Should().Be(2);
    }
}
