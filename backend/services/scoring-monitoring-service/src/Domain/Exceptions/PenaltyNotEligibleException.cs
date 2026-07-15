namespace umbral_backend.Domain.Exceptions;

public sealed class PenaltyNotEligibleException : DomainException
{
    public PenaltyNotEligibleException(Guid liveSessionId, Guid teamId, string reason)
        : base($"Penalty is not eligible for session '{liveSessionId}', team '{teamId}', reason '{reason}'.")
    {
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        Reason = reason;
    }

    public Guid LiveSessionId { get; }

    public Guid TeamId { get; }

    public string Reason { get; }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
