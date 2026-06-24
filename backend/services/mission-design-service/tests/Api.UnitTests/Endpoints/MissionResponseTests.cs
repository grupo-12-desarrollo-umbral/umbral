using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.Queries.GetDifficultyCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionReadiness;
using umbral_backend.Application.Missions.Queries.GetMissionRuntimePlan;
using umbral_backend.Web.Controllers;
using static umbral_backend.Web.Controllers.MissionsController;

namespace umbral_backend.Web.UnitTests.Endpoints;

public class MissionResponseTests
{
    [Fact]
    public void FromDto_WhenStatusIsDraft_SetsIsActiveTrueAndIsSourceReadyFalse()
    {
        var dto = new MissionDto(1, "Mission", "Description", "Advanced", 45, "Draft");

        var response = MissionsController.MissionResponse.FromDto(dto);

        response.Id.Should().Be(1);
        response.IsActive.Should().BeTrue();
        response.ActivationState.Should().Be("Draft");
        response.IsSourceReady.Should().BeFalse();
    }

    [Fact]
    public void FromDto_WhenStatusIsReady_SetsIsActiveTrueAndIsSourceReadyTrue()
    {
        var dto = new MissionDto(2, "Mission", "Description", "Beginner", 30, "Ready");

        var response = MissionsController.MissionResponse.FromDto(dto);

        response.Id.Should().Be(2);
        response.IsActive.Should().BeTrue();
        response.ActivationState.Should().Be("Ready");
        response.IsSourceReady.Should().BeTrue();
    }

    [Fact]
    public void FromDto_WhenStatusIsInactive_SetsIsActiveFalseAndIsSourceReadyFalse()
    {
        var dto = new MissionDto(3, "Mission", "Description", "Advanced", 60, "Inactive");

        var response = MissionsController.MissionResponse.FromDto(dto);

        response.Id.Should().Be(3);
        response.IsActive.Should().BeFalse();
        response.ActivationState.Should().Be("Inactive");
        response.IsSourceReady.Should().BeFalse();
    }
}

public class DifficultyResponseTests
{
    [Fact]
    public void FromDto_MapsValueCorrectly()
    {
        var response = DifficultyResponse.FromDto(new DifficultyDto("Advanced"));

        response.Value.Should().Be("Advanced");
    }
}

public class MissionStageResponseTests
{
    [Fact]
    public void FromDto_MapsIdTitleSequenceOrderAndSubstages()
    {
        var substageDto = new MissionSubstageDto(5, "Sub", 1, "TreasureHunt", null, null, null, null);
        var dto = new MissionStageDto(3, "Stage", 2, [substageDto]);

        var response = MissionStageResponse.FromDto(dto);

        response.Id.Should().Be(3);
        response.Title.Should().Be("Stage");
        response.SequenceOrder.Should().Be(2);
        response.Substages.Should().ContainSingle();
    }

    [Fact]
    public void FromDto_NullSubstages_MapsToEmpty()
    {
        var dto = new MissionStageDto(1, "Stage", 1, null);

        var response = MissionStageResponse.FromDto(dto);

        response.Substages.Should().BeEmpty();
    }
}

public class MissionSubstageResponseTests
{
    [Fact]
    public void FromDto_WithTriviaQuizSelection_MapsSelection()
    {
        var selectionDto = new TriviaQuizSelectionDto(7);
        var dto = new MissionSubstageDto(2, "Trivia Sub", 1, "Trivia", null, selectionDto, null, null);

        var response = MissionSubstageResponse.FromDto(dto);

        response.TriviaQuizSelection.Should().NotBeNull();
        response.TriviaQuizSelection!.TriviaQuizId.Should().Be(7);
    }

    [Fact]
    public void FromDto_WithNullTriviaQuizSelection_MapsToNull()
    {
        var dto = new MissionSubstageDto(1, "Sub", 1, "TreasureHunt", null, null, null, null);

        var response = MissionSubstageResponse.FromDto(dto);

        response.TriviaQuizSelection.Should().BeNull();
    }
}

public class MissionTargetResponseTests
{
    [Fact]
    public void FromDto_MapsAllFields()
    {
        var dto = new MissionTargetDto(10, "Target A", "QR-A", 1, true, 40);

        var response = MissionTargetResponse.FromDto(dto);

        response.Id.Should().Be(10);
        response.Name.Should().Be("Target A");
        response.QrCode.Should().Be("QR-A");
        response.SequenceOrder.Should().Be(1);
        response.IsActive.Should().BeTrue();
        response.ClueId.Should().Be(40);
    }
}

public class MissionClueResponseTests
{
    [Fact]
    public void FromDto_MapsAllFields()
    {
        var dto = new MissionClueDto(5, "Clue Title", 1, "Look around.", "VisibleWhenSubstageStarts");

        var response = MissionClueResponse.FromDto(dto);

        response.Id.Should().Be(5);
        response.Title.Should().Be("Clue Title");
        response.SequenceOrder.Should().Be(1);
        response.Text.Should().Be("Look around.");
        response.VisibilityPolicy.Should().Be("VisibleWhenSubstageStarts");
    }
}

public class TriviaQuizSelectionResponseTests
{
    [Fact]
    public void FromDto_MapsTriviaQuizId()
    {
        var response = TriviaQuizSelectionResponse.FromDto(new TriviaQuizSelectionDto(12));

        response.TriviaQuizId.Should().Be(12);
    }
}

public class MissionReadinessResponseTests
{
    [Fact]
    public void FromDto_WhenReady_MapsIsReadyTrue()
    {
        var dto = new MissionReadinessDto(3, "Ready", true, null);

        var response = MissionReadinessResponse.FromDto(dto);

        response.MissionId.Should().Be(3);
        response.ActivationState.Should().Be("Ready");
        response.IsReady.Should().BeTrue();
        response.Failures.Should().BeEmpty();
    }

    [Fact]
    public void FromDto_WhenNotReady_MapsFailures()
    {
        var dto = new MissionReadinessDto(4, "Draft", false, ["Must have a stage."]);

        var response = MissionReadinessResponse.FromDto(dto);

        response.IsReady.Should().BeFalse();
        response.Failures.Should().ContainSingle("Must have a stage.");
    }
}

public class MissionSummaryResponseTests
{
    [Fact]
    public void FromDto_WhenStatusIsDraft_SetsIsActiveTrueAndIsSourceReadyFalse()
    {
        var dto = new MissionSummaryDto(1, "Mission", "Description", "Advanced", "Draft");

        var response = MissionsController.MissionSummaryResponse.FromDto(dto);

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

        var response = MissionsController.MissionSummaryResponse.FromDto(dto);

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

        var response = MissionsController.MissionSummaryResponse.FromDto(dto);

        response.Id.Should().Be(3);
        response.Difficulty.Should().Be("Advanced");
        response.IsActive.Should().BeFalse();
        response.ActivationState.Should().Be("Inactive");
        response.IsSourceReady.Should().BeFalse();
    }
}
