using umbral_backend.Domain.Common;

namespace umbral_backend.Application.UnitTests.Domain.Common;

// Covers the two guard branches of ValueObject.Equals(object?) that the happy-path equality tests
// never reach: a null argument and an argument of a different runtime type.
public sealed class ValueObjectEqualsBranchesTests
{
    private sealed class Sample(int value) : ValueObject
    {
        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return value;
        }
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var value = new Sample(1);

        value.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_DifferentRuntimeType_ReturnsFalse()
    {
        var value = new Sample(1);

        value.Equals("not a value object").Should().BeFalse();
    }
}
