namespace umbral_backend.Domain.Exceptions;

public sealed class ParticipantLockedToAnotherSessionTeamException : DomainException
{
    public ParticipantLockedToAnotherSessionTeamException(int userId, Guid currentTeamId, Guid requestedTeamId)
        : base(
            $"Participant '{userId}' is already assigned to team '{currentTeamId}' in this session and cannot join team '{requestedTeamId}'.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
