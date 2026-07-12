using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.SessionOperations.UnitTests.Domain.ValueObjects;

public sealed class TargetSnapshotTests
{
    [Fact]
    public void Create_WithoutSubstageSnapshotId_ThrowsException()
    {
        var act = () => TargetSnapshot.Create(
            Guid.Empty,
            "Main Exhibit",
            "QR-001",
            1,
            true,
            100,
            4.711,
            -74.0721,
            "Look near the entrance.",
            "VisibleAtStart");

        act.Should().Throw<TargetSnapshotSubstageRequiredException>();
    }

    [Fact]
    public void Create_CarriesCoordinatesImmutably()
    {
        var target = TargetSnapshot.Create(
            Guid.NewGuid(),
            "Main Exhibit",
            "QR-001",
            1,
            true,
            100,
            4.711,
            -74.0721,
            "Look near the entrance.",
            "VisibleAtStart");

        target.Latitude.Should().Be(4.711);
        target.Longitude.Should().Be(-74.0721);
    }
}
