namespace umbral_backend.Domain.Exceptions;

public sealed class PenaltyRequiresReasonException : DomainException
{
    public PenaltyRequiresReasonException(string? attemptedReason)
        : base("Penalty reason is required and cannot be blank.")
    {
        AttemptedReason = attemptedReason;
    }

    public string? AttemptedReason { get; }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
