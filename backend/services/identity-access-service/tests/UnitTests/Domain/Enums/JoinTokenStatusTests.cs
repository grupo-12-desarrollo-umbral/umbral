using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Domain.Enums;

public sealed class JoinTokenStatusTests
{
    [Fact]
    public void JoinTokenStatus_DefinesExpectedLifecycleValues()
    {
        Enum.GetValues<JoinTokenStatus>().Should().Equal(
            JoinTokenStatus.Active,
            JoinTokenStatus.Consumed,
            JoinTokenStatus.Expired,
            JoinTokenStatus.Revoked);
    }
}
