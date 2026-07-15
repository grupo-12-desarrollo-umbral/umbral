using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class PenaltyReasonTests
{
    [Fact]
    public void Create_ShouldRejectBlankValues()
    {
        var actNull = () => PenaltyReason.Create(null!);
        var actEmpty = () => PenaltyReason.Create(string.Empty);
        var actWhitespace = () => PenaltyReason.Create("   ");

        actNull.Should().Throw<PenaltyRequiresReasonException>()
            .Which.AttemptedReason.Should().BeNull();
        actEmpty.Should().Throw<PenaltyRequiresReasonException>();
        actWhitespace.Should().Throw<PenaltyRequiresReasonException>();
    }

    [Fact]
    public void Create_ShouldAllowNonBlankValue()
    {
        var reason = PenaltyReason.Create("Unsportsmanlike conduct");

        reason.Value.Should().Be("Unsportsmanlike conduct");
    }

    [Fact]
    public void Equality_ShouldBeBasedOnValue()
    {
        var a = PenaltyReason.Create("Late submission");
        var b = PenaltyReason.Create("Late submission");
        var c = PenaltyReason.Create("Other reason");

        a.Should().Be(b);
        a.Should().NotBe(c);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
