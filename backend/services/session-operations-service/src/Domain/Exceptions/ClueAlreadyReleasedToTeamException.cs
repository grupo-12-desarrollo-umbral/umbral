namespace umbral_backend.Domain.Exceptions;

public sealed class ClueAlreadyReleasedToTeamException : DomainException
{
    public ClueAlreadyReleasedToTeamException(Guid teamId, Guid targetId)
        : base($"The clue for target '{targetId}' has already been released to team '{teamId}'.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
