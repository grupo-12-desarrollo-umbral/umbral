namespace umbral_backend.Domain.Exceptions;

public sealed class ParticipantAlreadyAuthorizedForTeamException : DomainException
{
    public ParticipantAlreadyAuthorizedForTeamException(Guid teamId, int userId)
        : base($"Participant '{userId}' is already authorized for team '{teamId}'.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
