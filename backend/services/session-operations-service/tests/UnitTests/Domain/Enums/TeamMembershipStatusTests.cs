using umbral_backend.Domain.Enums;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Enums;

public sealed class TeamMembershipStatusTests
{
    [Fact]
    public void Active_HasStableValue()
    {
        ((int)TeamMembershipStatus.Active).Should().Be(2);
    }
}
