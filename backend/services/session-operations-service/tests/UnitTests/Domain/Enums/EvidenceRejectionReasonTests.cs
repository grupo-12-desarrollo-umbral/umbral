using umbral_backend.Domain.Enums;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Enums;

public sealed class EvidenceRejectionReasonTests
{
    [Fact]
    public void SubstageBindingMismatch_HasStableValue()
    {
        ((int)EvidenceRejectionReason.SubstageBindingMismatch).Should().Be(0);
    }

    [Fact]
    public void OutsideSubmissionWindow_HasStableValue()
    {
        ((int)EvidenceRejectionReason.OutsideSubmissionWindow).Should().Be(1);
    }

    [Fact]
    public void UnauthorizedOrigin_HasStableValue()
    {
        ((int)EvidenceRejectionReason.UnauthorizedOrigin).Should().Be(2);
    }
}
