using umbral_backend.Domain.Enums;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Enums;

public sealed class TeamJoinStatusTests
{
    [Fact]
    public void Closed_HasStableValue()
    {
        ((int)TeamJoinStatus.Closed).Should().Be(3);
    }
}
