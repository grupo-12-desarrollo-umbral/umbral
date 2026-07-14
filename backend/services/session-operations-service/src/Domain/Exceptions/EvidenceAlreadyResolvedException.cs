using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class EvidenceAlreadyResolvedException : DomainException
{
    public EvidenceAlreadyResolvedException(EvidenceValidationState validationState)
        : base($"Evidence in state '{validationState}' has already been resolved.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
