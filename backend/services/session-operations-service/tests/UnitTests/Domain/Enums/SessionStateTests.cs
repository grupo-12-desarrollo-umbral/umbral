using umbral_backend.Domain.Enums;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Enums;

public sealed class SessionStateTests
{
    [Fact]
    public void Active_HasStableValue()
    {
        ((int)SessionState.Active).Should().Be(3);
    }
}
