using umbral_backend.Domain.Common;

namespace umbral_backend.Application.UnitTests.Domain.Common;

public sealed class ValueObjectTests
{
    [Fact]
    public void EqualityMembers_UseEqualityComponents()
    {
        var first = SampleValueObject.Create("alpha", 1);
        var second = SampleValueObject.Create("alpha", 1);
        var different = SampleValueObject.Create("beta", 2);

        first.Equals(second).Should().BeTrue();
        (first == second).Should().BeTrue();
        (first != second).Should().BeFalse();
        first.GetHashCode().Should().Be(second.GetHashCode());
        first.Equals(different).Should().BeFalse();
    }

    [Fact]
    public void EqualityOperators_HandleNullValues()
    {
        ValueObject? left = null;
        ValueObject? right = null;
        var value = SampleValueObject.Create("alpha", 1);

        (left == right).Should().BeTrue();
        (value == left!).Should().BeFalse();
        (value != left!).Should().BeTrue();
    }

    private sealed class SampleValueObject : ValueObject
    {
        private SampleValueObject(string name, int number)
        {
            Name = name;
            Number = number;
        }

        public string Name { get; }

        public int Number { get; }

        public static SampleValueObject Create(string name, int number) => new(name, number);

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Name;
            yield return Number;
        }
    }
}
