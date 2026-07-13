namespace umbral_backend.Domain.Enums;

// Validation outcome of an EvidenceSubmission. Shared intake registers Pending; each concrete form
// retains ownership of its own acceptance or rejection behavior.
public enum EvidenceValidationState
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2
}
