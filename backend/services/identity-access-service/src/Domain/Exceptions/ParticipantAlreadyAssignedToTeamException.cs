namespace umbral_backend.Domain.Exceptions;

public sealed class ParticipantAlreadyAssignedToTeamException : DomainException
{
    public ParticipantAlreadyAssignedToTeamException(Guid teamId, int userId)
        : base($"Participant '{userId}' is already assigned to team '{teamId}'.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
