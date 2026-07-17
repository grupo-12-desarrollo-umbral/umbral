namespace umbral_backend.Domain.Exceptions;

// Base-evidence invariant (bd_umbral_entity_spec.md: EvidenceSubmission Key Constraints): every
// submission records when it was submitted, so an unset (default) timestamp — which would persist a
// year-1 reading — is never a valid server clock value.
public sealed class EvidenceSubmissionTimestampRequiredException : DomainException
{
    public EvidenceSubmissionTimestampRequiredException()
        : base("An evidence submission must record the instant it was submitted.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
