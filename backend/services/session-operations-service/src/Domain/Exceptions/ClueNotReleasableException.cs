namespace umbral_backend.Domain.Exceptions;

public sealed class ClueNotReleasableException : DomainException
{
    public ClueNotReleasableException(Guid targetId)
        : base($"Target '{targetId}' does not have a hidden clue in the active treasure-hunt substage.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
