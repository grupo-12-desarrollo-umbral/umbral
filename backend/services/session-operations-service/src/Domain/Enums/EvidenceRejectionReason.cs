namespace umbral_backend.Domain.Enums;

// Contextual reasons that explain why a pending evidence submission could not be accepted.
public enum EvidenceRejectionReason
{
    SubstageBindingMismatch = 0,
    OutsideSubmissionWindow = 1,
    UnauthorizedOrigin = 2
}
