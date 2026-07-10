namespace umbral_backend.Domain.Enums;

// Validation outcome of an EvidenceSubmission. HU-34 only ever persists Accepted answers
// (rejections throw and never create a submission); Rejected exists for the umbrella base and
// later evidence forms that carry an operator review decision.
public enum EvidenceValidationState
{
    Accepted = 1,
    Rejected = 2
}
