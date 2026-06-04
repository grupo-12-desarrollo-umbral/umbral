using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.SessionOperations.UnitTests.Domain.ValueObjects;

public sealed class AuthoritativeSessionTimerSnapshotTests
{
    [Fact]
    public void Create_WithEquivalentValues_ComparesByTimerState()
    {
        var observedAt = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);
        var advancingSince = observedAt.AddMinutes(-2);

        var first = AuthoritativeSessionTimerSnapshot.Create(
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(8),
            isAdvancing: true,
            observedAt,
            advancingSince,
            expiredAt: null);
        var second = AuthoritativeSessionTimerSnapshot.Create(
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(8),
            isAdvancing: true,
            observedAt,
            advancingSince,
            expiredAt: null);

        first.Should().Be(second);
    }

    [Fact]
    public void IsExpired_WhenRemainingTimeIsZero_ReturnsTrue()
    {
        var snapshot = AuthoritativeSessionTimerSnapshot.Create(
            TimeSpan.FromMinutes(10),
            TimeSpan.Zero,
            isAdvancing: false,
            new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero),
            advancingSince: null,
            expiredAt: new DateTimeOffset(2026, 6, 3, 10, 10, 0, TimeSpan.Zero));

        snapshot.IsExpired.Should().BeTrue();
    }
}
