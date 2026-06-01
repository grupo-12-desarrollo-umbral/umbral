using umbral_backend.Web.Services;

namespace umbral_backend.Web.UnitTests.Services;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_ReturnsCurrentUtcTime()
    {
        var clock = new SystemClock();
        var before = DateTimeOffset.UtcNow;

        var now = clock.UtcNow;

        now.Should().BeCloseTo(before, TimeSpan.FromSeconds(1));
        now.Offset.Should().Be(TimeSpan.Zero);
    }
}
