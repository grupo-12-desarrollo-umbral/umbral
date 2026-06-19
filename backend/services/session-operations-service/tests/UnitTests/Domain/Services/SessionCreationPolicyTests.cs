using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Services;

public sealed class SessionCreationPolicyTests
{
    private const int MissionId = 7;
    private readonly SessionCreationPolicy _policy = new();

    [Fact]
    public void EnsureMissionEligible_WhenActiveAndReady_DoesNotThrow()
    {
        var act = () => _policy.EnsureMissionEligible(MissionId, isActive: true, isReady: true);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureMissionEligible_WhenInactive_ThrowsNotEligible()
    {
        var act = () => _policy.EnsureMissionEligible(MissionId, isActive: false, isReady: true);

        act.Should()
            .Throw<MissionNotEligibleForSessionCreationException>()
            .Which.MissionId.Should().Be(MissionId);
    }

    [Fact]
    public void EnsureMissionEligible_WhenInactiveTakesPrecedenceOverReadiness()
    {
        var act = () => _policy.EnsureMissionEligible(MissionId, isActive: false, isReady: false);

        act.Should()
            .Throw<MissionNotEligibleForSessionCreationException>()
            .Which.Reason.Should().Contain("inactive");
    }

    [Fact]
    public void EnsureMissionEligible_WhenActiveButNotReady_ThrowsNotEligible()
    {
        var act = () => _policy.EnsureMissionEligible(MissionId, isActive: true, isReady: false);

        act.Should()
            .Throw<MissionNotEligibleForSessionCreationException>()
            .Which.Reason.Should().Contain("runtime-ready");
    }
}
