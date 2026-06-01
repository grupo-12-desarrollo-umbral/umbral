using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Domain.ValueObjects;

public class MaximumTimeTests
{
    [Fact]
    public void Create_ReturnsMinutes()
    {
        var maximumTime = MaximumTime.Create(45);

        maximumTime.Minutes.Should().Be(45);
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
    public void Equals_MaximumTimesWithSameMinutes_AreEqual()
    {
        var a = MaximumTime.Create(45);
        var b = MaximumTime.Create(45);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equals_MaximumTimesWithDifferentMinutes_AreNotEqual()
    {
        var a = MaximumTime.Create(45);
        var b = MaximumTime.Create(30);

        a.Should().NotBe(b);
    }

    [Fact]
    public void Equals_ComparedToNull_ReturnsFalse()
    {
        var maxTime = MaximumTime.Create(45);

        maxTime.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_ComparedToDifferentType_ReturnsFalse()
    {
        var maxTime = MaximumTime.Create(45);

        maxTime.Equals(45).Should().BeFalse();
    }

    [Fact]
    public void EqualityOperator_NullComparison_ReturnsFalse()
    {
        MaximumTime? nil = null;
        var a = MaximumTime.Create(45);

        (a == nil).Should().BeFalse();
        (nil == a).Should().BeFalse();
    }

    [Fact]
    public void EqualityOperator_BothNull_ReturnsTrue()
    {
        MaximumTime? a = null;
        MaximumTime? b = null;

        (a == b).Should().BeTrue();
    }

    [Fact]
    public void InequalityOperator_SameValue_ReturnsFalse()
    {
        var a = MaximumTime.Create(45);
        var b = MaximumTime.Create(45);

        (a != b).Should().BeFalse();
    }
}
