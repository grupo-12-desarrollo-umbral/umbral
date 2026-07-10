using umbral_backend.Domain.Enums;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Enums;

public sealed class EvidenceValidationStateTests
{
    [Fact]
    public void Accepted_HasStableValue()
    {
        ((int)EvidenceValidationState.Accepted).Should().Be(1);
    }

    [Fact]
    public void Rejected_HasStableValue()
    {
        ((int)EvidenceValidationState.Rejected).Should().Be(2);
    }
}
