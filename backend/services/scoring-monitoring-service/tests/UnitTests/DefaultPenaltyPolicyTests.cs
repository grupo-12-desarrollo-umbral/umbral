using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class DefaultPenaltyPolicyTests
{
    [Fact]
    public void ValidateEligibility_ShouldAcceptValidInputs()
    {
        var policy = new DefaultPenaltyPolicy();
        var sessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var act = () => policy.ValidateEligibility(sessionId, teamId, "Unsportsmanlike conduct");

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateEligibility_ShouldRejectEmptySessionId()
    {
        var policy = new DefaultPenaltyPolicy();

        var act = () => policy.ValidateEligibility(Guid.Empty, Guid.NewGuid(), "Valid reason");

        act.Should().Throw<PenaltyNotEligibleException>()
            .Which.LiveSessionId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void ValidateEligibility_ShouldRejectEmptyTeamId()
    {
        var policy = new DefaultPenaltyPolicy();

        var act = () => policy.ValidateEligibility(Guid.NewGuid(), Guid.Empty, "Valid reason");

        act.Should().Throw<PenaltyNotEligibleException>()
            .Which.TeamId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void ValidateEligibility_ShouldRejectBlankReason()
    {
        var policy = new DefaultPenaltyPolicy();

        var act = () => policy.ValidateEligibility(Guid.NewGuid(), Guid.NewGuid(), "   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DefaultPenaltyPolicy_ShouldBeSealed()
    {
        typeof(DefaultPenaltyPolicy).IsSealed.Should().BeTrue();
    }
}
