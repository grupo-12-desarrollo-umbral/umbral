using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Domain.Enums;

public class MissionDesignEnumsTests
{
    [Fact]
    public void SubstagePlayMode_HasExactlyTreasureHuntAndTrivia()
    {
        Enum.GetValues<SubstagePlayMode>()
            .Should().BeEquivalentTo(new[] { SubstagePlayMode.TreasureHunt, SubstagePlayMode.Trivia });
    }

    [Fact]
    public void MissionNodeType_HasStageSubstageClue()
    {
        Enum.GetValues<MissionNodeType>()
            .Should().BeEquivalentTo(new[] { MissionNodeType.Stage, MissionNodeType.Substage, MissionNodeType.Clue });
    }

    [Fact]
    public void ClueVisibilityPolicy_HasVisibleAndHiddenOptions()
    {
        Enum.GetValues<ClueVisibilityPolicy>()
            .Should().BeEquivalentTo(new[]
            {
                ClueVisibilityPolicy.VisibleWhenSubstageStarts,
                ClueVisibilityPolicy.HiddenUntilOperatorRelease,
            });
    }

    [Fact]
    public void MissionActivation_HasDraftReadyInactive()
    {
        Enum.GetValues<MissionActivation>()
            .Should().BeEquivalentTo(new[]
            {
                MissionActivation.Draft,
                MissionActivation.Ready,
                MissionActivation.Inactive,
            });
    }
}
