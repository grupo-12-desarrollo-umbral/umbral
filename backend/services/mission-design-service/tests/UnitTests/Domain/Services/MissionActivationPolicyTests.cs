using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Domain.Services;

public class MissionActivationPolicyTests
{
    [Theory]
    [InlineData(true, false, MissionActivation.Draft)]
    [InlineData(true, true, MissionActivation.Ready)]
    [InlineData(false, false, MissionActivation.Inactive)]
    [InlineData(false, true, MissionActivation.Inactive)]
    public void DetermineActivationState_ReturnsExpectedMissionActivation(
        bool isActive,
        bool satisfiesStructureRequirements,
        MissionActivation expected)
    {
        var result = MissionActivationPolicy.DetermineActivationState(isActive, satisfiesStructureRequirements);

        result.Should().Be(expected);
    }
}
