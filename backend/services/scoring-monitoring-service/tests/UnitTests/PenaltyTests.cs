using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class PenaltyTests
{
    [Fact]
    public void Create_ShouldProduceValidPenalty()
    {
        var scoreEntryId = Guid.NewGuid();
        var appliedByUserId = Guid.NewGuid();

        var penalty = Penalty.Create(scoreEntryId, "Unsportsmanlike conduct", appliedByUserId);

        penalty.PenaltyId.Should().NotBe(Guid.Empty);
        penalty.ScoreEntryId.Should().Be(scoreEntryId);
        penalty.PenaltyReason.Value.Should().Be("Unsportsmanlike conduct");
        penalty.AppliedByUserId.Should().Be(appliedByUserId);
        penalty.AppliedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_ShouldRejectBlankReason()
    {
        var act = () => Penalty.Create(Guid.NewGuid(), "   ", Guid.NewGuid());

        act.Should().Throw<PenaltyRequiresReasonException>();
    }

    [Fact]
    public void Penalty_ShouldBeSealed()
    {
        typeof(Penalty).IsSealed.Should().BeTrue();
    }
}
