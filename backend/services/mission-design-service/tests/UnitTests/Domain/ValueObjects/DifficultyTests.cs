using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Domain.ValueObjects;

public class DifficultyTests
{
    [Fact]
    public void Create_TrimsValue()
    {
        var difficulty = Difficulty.Create(" Advanced ");

        difficulty.Value.Should().Be("Advanced");
    }

    [Fact]
    public void Create_WhenValueIsBlank_Throws()
    {
        var act = () => Difficulty.Create(" ");

        act.Should().Throw<DifficultyValueRequiredException>();
    }

    [Fact]
    public void Create_WhenValueIsNull_Throws()
    {
        var act = () => Difficulty.Create(null!);

        act.Should().Throw<DifficultyValueRequiredException>();
    }

    [Fact]
    public void Equals_DifficultiesWithSameValue_AreEqual()
    {
        var a = Difficulty.Create("Advanced");
        var b = Difficulty.Create("Advanced");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equals_DifficultiesWithDifferentValue_AreNotEqual()
    {
        var a = Difficulty.Create("Advanced");
        var b = Difficulty.Create("Beginner");

        a.Should().NotBe(b);
    }

    [Fact]
    public void Equals_ComparedToNull_ReturnsFalse()
    {
        var difficulty = Difficulty.Create("Advanced");

        difficulty.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_ComparedToDifferentType_ReturnsFalse()
    {
        var difficulty = Difficulty.Create("Advanced");

        difficulty.Equals("Advanced").Should().BeFalse();
    }

    [Fact]
    public void EqualityOperator_NullComparison_ReturnsFalse()
    {
        Difficulty? nil = null;
        var a = Difficulty.Create("Advanced");

        (a == nil!).Should().BeFalse();
        (nil! == a).Should().BeFalse();
    }

    [Fact]
    public void EqualityOperator_BothNull_ReturnsTrue()
    {
        Difficulty? a = null;
        Difficulty? b = null;

        (a! == b!).Should().BeTrue();
    }

    [Fact]
    public void InequalityOperator_SameValue_ReturnsFalse()
    {
        var a = Difficulty.Create("Advanced");
        var b = Difficulty.Create("Advanced");

        (a != b).Should().BeFalse();
    }

    [Theory]
    [InlineData("Beginner", 1)]
    [InlineData("Intermediate", 2)]
    [InlineData("Advanced", 3)]
    [InlineData("advanced", 3)]
    public void ScoreFactor_IsTheOneBasedTier(string value, int expected)
    {
        Difficulty.Create(value).ScoreFactor.Should().Be(expected);
    }
}
