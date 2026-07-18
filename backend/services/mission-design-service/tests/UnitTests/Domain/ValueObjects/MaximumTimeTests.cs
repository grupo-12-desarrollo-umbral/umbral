using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Domain.ValueObjects;

public class MaximumTimeTests
{
    [Fact]
    public void Create_ReturnsMinutes()
    {
        var maximumTime = MaximumTime.Create(25);

        maximumTime.Minutes.Should().Be(25);
    }

    [Fact]
    public void Create_AtTheMaximumLimit_Succeeds()
    {
        var maximumTime = MaximumTime.Create(MaximumTime.MaximumMinutes);

        maximumTime.Minutes.Should().Be(MaximumTime.MaximumMinutes);
    }

    [Fact]
    public void Create_WhenMinutesAreNotPositive_Throws()
    {
        var act = () => MaximumTime.Create(0);

        act.Should().Throw<MaximumTimeMustBePositiveException>();
    }

    [Fact]
    public void Create_WhenMinutesAreNegative_Throws()
    {
        var act = () => MaximumTime.Create(-1);

        act.Should().Throw<MaximumTimeMustBePositiveException>();
    }

    [Fact]
    public void Create_WhenMinutesExceedTheLimit_Throws()
    {
        var act = () => MaximumTime.Create(MaximumTime.MaximumMinutes + 1);

        act.Should().Throw<MaximumTimeExceedsLimitException>();
    }

    [Fact]
    public void Equals_MaximumTimesWithSameMinutes_AreEqual()
    {
        var a = MaximumTime.Create(25);
        var b = MaximumTime.Create(25);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equals_MaximumTimesWithDifferentMinutes_AreNotEqual()
    {
        var a = MaximumTime.Create(25);
        var b = MaximumTime.Create(30);

        a.Should().NotBe(b);
    }

    [Fact]
    public void Equals_ComparedToNull_ReturnsFalse()
    {
        var maxTime = MaximumTime.Create(25);

        maxTime.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_ComparedToDifferentType_ReturnsFalse()
    {
        var maxTime = MaximumTime.Create(25);

        maxTime.Equals(25).Should().BeFalse();
    }

    [Fact]
    public void EqualityOperator_NullComparison_ReturnsFalse()
    {
        MaximumTime? nil = null;
        var a = MaximumTime.Create(25);

        (a == nil!).Should().BeFalse();
        (nil! == a).Should().BeFalse();
    }

    [Fact]
    public void EqualityOperator_BothNull_ReturnsTrue()
    {
        MaximumTime? a = null;
        MaximumTime? b = null;

        (a! == b!).Should().BeTrue();
    }

    [Fact]
    public void InequalityOperator_SameValue_ReturnsFalse()
    {
        var a = MaximumTime.Create(25);
        var b = MaximumTime.Create(25);

        (a != b).Should().BeFalse();
    }
}
