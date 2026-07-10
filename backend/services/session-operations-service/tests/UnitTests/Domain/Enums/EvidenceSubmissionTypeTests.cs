using umbral_backend.Domain.Enums;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Enums;

public sealed class EvidenceSubmissionTypeTests
{
    [Fact]
    public void TreasureHuntQrScan_HasStableValue()
    {
        ((int)EvidenceSubmissionType.TreasureHuntQrScan).Should().Be(1);
    }

    [Fact]
    public void TriviaAnswer_HasStableValue()
    {
        ((int)EvidenceSubmissionType.TriviaAnswer).Should().Be(2);
    }
}
