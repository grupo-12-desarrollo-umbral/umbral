namespace umbral_backend.Domain.Exceptions;

public sealed class ClueAlreadyReleasedToTeamException : DomainException
{
    public ClueAlreadyReleasedToTeamException(Guid teamId, Guid subjectId)
        : base($"Clue '{subjectId}' has already been released to team '{teamId}'.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
