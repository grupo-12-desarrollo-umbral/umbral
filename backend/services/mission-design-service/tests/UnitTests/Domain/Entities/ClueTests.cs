using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Domain.Entities;

// Covers Clue.Create's text handling: a null body is coalesced to empty rather than throwing,
// while a real body is trimmed.
public class ClueTests
{
    [Fact]
    public void Create_WhenTextIsNull_CoalescesToEmpty()
    {
        var clue = Clue.Create("Look north", 1, text: null!);

        clue.Text.Should().BeEmpty();
        clue.NodeType.Should().Be(MissionNodeType.Clue);
        clue.Visibility.Should().Be(ClueVisibilityPolicy.HiddenUntilOperatorRelease);
    }

    [Fact]
    public void Create_WhenTextProvided_TrimsBody()
    {
        var clue = Clue.Create("Look north", 1, "  Behind the oak  ", ClueVisibilityPolicy.VisibleWhenSubstageStarts);

        clue.Text.Should().Be("Behind the oak");
        clue.Visibility.Should().Be(ClueVisibilityPolicy.VisibleWhenSubstageStarts);
    }
}
