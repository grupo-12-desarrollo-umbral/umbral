using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Common.Exceptions;

/// <summary>
/// Internal short-circuit signal for contextual evidence validation. The intake Facade translates
/// this signal into a recorded domain rejection; it must not escape to the API boundary.
/// </summary>
public sealed class EvidenceContextRejectedException : Exception
{
    public EvidenceContextRejectedException(EvidenceRejectionReason reason)
        : base($"The evidence context was rejected: {reason}.")
    {
        Reason = reason;
    }

    public EvidenceRejectionReason Reason { get; }
}
