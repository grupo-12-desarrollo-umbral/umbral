using umbral_backend.Domain.Common;

namespace umbral_backend.Domain.UnitTests.Common;

// Covers ValueObject.Equals guard branches (null argument, different runtime type) and the
// GetHashCode null-component fallback (component?.GetHashCode() ?? 0).
public sealed class ValueObjectBranchTests
{
    private sealed class Sample(params object?[] components) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents() => components;
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        new Sample(1).Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_DifferentRuntimeType_ReturnsFalse()
    {
        new Sample(1).Equals("not a value object").Should().BeFalse();
    }

    [Fact]
    public void Equals_SameComponents_ReturnsTrue()
    {
        new Sample(1, "a").Equals(new Sample(1, "a")).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_WithNullComponent_UsesZeroFallback()
    {
        // A null component must hash to 0 rather than throw; equal shapes still agree.
        var withNull = new Sample(null, "x");
        var alsoNull = new Sample(null, "x");

        withNull.GetHashCode().Should().Be(alsoNull.GetHashCode());
    }

    [Fact]
    public void EqualityOperators_HandleNulls()
    {
        Sample? left = null;
        (left == null).Should().BeTrue();
        (new Sample(1) != null).Should().BeTrue();
    }
}
