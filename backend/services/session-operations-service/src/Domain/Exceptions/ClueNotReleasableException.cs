namespace umbral_backend.Domain.Exceptions;

public sealed class ClueNotReleasableException : DomainException
{
    public ClueNotReleasableException(Guid subjectId)
        : base($"Clue release subject '{subjectId}' does not identify a hidden clue in the active substage.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
