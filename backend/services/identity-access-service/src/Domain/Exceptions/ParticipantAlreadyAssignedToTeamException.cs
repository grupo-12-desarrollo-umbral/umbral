namespace umbral_backend.Domain.Exceptions;

public sealed class ParticipantAlreadyAssignedToTeamException : Exception
{
    public ParticipantAlreadyAssignedToTeamException(Guid teamId, int userId)
        : base($"Participant '{userId}' is already assigned to team '{teamId}'.")
    {
    }
}
