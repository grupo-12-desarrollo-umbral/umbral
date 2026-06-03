using umbral_backend.Domain.Enums;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Enums;

public sealed class JoinContextStatusTests
{
    [Fact]
    public void Consumed_HasStableValue()
    {
        ((int)JoinContextStatus.Consumed).Should().Be(2);
    }
}
