namespace umbral_backend.Domain.Exceptions;

// Base-evidence invariant (bd_umbral_entity_spec.md: EvidenceSubmission Key Constraints): every
// submission must reference exactly one LiveSession, one Team, and one active substage.
public sealed class EvidenceSubmissionContextRequiredException : DomainException
{
    public EvidenceSubmissionContextRequiredException()
        : base("An evidence submission must reference a live session, a team, and an active substage.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
