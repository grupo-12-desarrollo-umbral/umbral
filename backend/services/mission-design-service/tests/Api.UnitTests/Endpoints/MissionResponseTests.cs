using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Web.Endpoints;

namespace umbral_backend.Web.UnitTests.Endpoints;

public class MissionResponseTests
{
    [Fact]
    public void FromDto_WhenStatusIsDraft_SetsIsActiveTrueAndIsSourceReadyFalse()
    {
        var dto = new MissionDto(1, "Mission", "Description", "Advanced", 45, "Draft");

        var response = MissionsEndpoints.MissionResponse.FromDto(dto);

        response.Id.Should().Be(1);
        response.IsActive.Should().BeTrue();
        response.ActivationState.Should().Be("Draft");
        response.IsSourceReady.Should().BeFalse();
    }

    [Fact]
    public void FromDto_WhenStatusIsReady_SetsIsActiveTrueAndIsSourceReadyTrue()
    {
        var dto = new MissionDto(2, "Mission", "Description", "Beginner", 30, "Ready");

        var response = MissionsEndpoints.MissionResponse.FromDto(dto);

        response.Id.Should().Be(2);
        response.IsActive.Should().BeTrue();
        response.ActivationState.Should().Be("Ready");
        response.IsSourceReady.Should().BeTrue();
    }

    [Fact]
    public void FromDto_WhenStatusIsInactive_SetsIsActiveFalseAndIsSourceReadyFalse()
    {
        var dto = new MissionDto(3, "Mission", "Description", "Advanced", 60, "Inactive");

        var response = MissionsEndpoints.MissionResponse.FromDto(dto);

        response.Id.Should().Be(3);
        response.IsActive.Should().BeFalse();
        response.ActivationState.Should().Be("Inactive");
        response.IsSourceReady.Should().BeFalse();
    }
}

public class MissionSummaryResponseTests
{
    [Fact]
    public void FromDto_WhenStatusIsDraft_SetsIsActiveTrueAndIsSourceReadyFalse()
    {
        var dto = new MissionSummaryDto(1, "Mission", "Description", "Advanced", "Draft");

        var response = MissionsEndpoints.MissionSummaryResponse.FromDto(dto);

        response.Id.Should().Be(1);
        response.Difficulty.Should().Be("Advanced");
        response.IsActive.Should().BeTrue();
        response.ActivationState.Should().Be("Draft");
        response.IsSourceReady.Should().BeFalse();
    }

    [Fact]
    public void FromDto_WhenStatusIsReady_SetsIsActiveTrueAndIsSourceReadyTrue()
    {
        var dto = new MissionSummaryDto(2, "Mission", "Description", "Beginner", "Ready");

        var response = MissionsEndpoints.MissionSummaryResponse.FromDto(dto);

        response.Id.Should().Be(2);
        response.Difficulty.Should().Be("Beginner");
        response.IsActive.Should().BeTrue();
        response.ActivationState.Should().Be("Ready");
        response.IsSourceReady.Should().BeTrue();
    }

    [Fact]
    public void FromDto_WhenStatusIsInactive_SetsIsActiveFalseAndIsSourceReadyFalse()
    {
        var dto = new MissionSummaryDto(3, "Mission", "Description", "Advanced", "Inactive");

        var response = MissionsEndpoints.MissionSummaryResponse.FromDto(dto);

        response.Id.Should().Be(3);
        response.Difficulty.Should().Be("Advanced");
        response.IsActive.Should().BeFalse();
        response.ActivationState.Should().Be("Inactive");
        response.IsSourceReady.Should().BeFalse();
    }
}
