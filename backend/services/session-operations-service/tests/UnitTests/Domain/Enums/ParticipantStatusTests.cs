using umbral_backend.Domain.Enums;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Enums;

public sealed class ParticipantStatusTests
{
    [Fact]
    public void Disconnected_HasStableValue()
    {
        ((int)ParticipantStatus.Disconnected).Should().Be(3);
    }
}
