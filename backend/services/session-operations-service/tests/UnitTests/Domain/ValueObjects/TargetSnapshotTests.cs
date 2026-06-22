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
            "Look near the entrance.",
            "VisibleAtStart");

        act.Should().Throw<TargetSnapshotSubstageRequiredException>();
    }
}
