using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.SessionOperations.UnitTests.Domain.ValueObjects;

public sealed class TriviaOptionSnapshotTests
{
    [Fact]
    public void Create_WithSameData_IsEqual()
    {
        var left = TriviaOptionSnapshot.Create(" Mercury ", 1, false);
        var right = TriviaOptionSnapshot.Create("Mercury", 1, false);

        left.Should().Be(right);
    }
}
