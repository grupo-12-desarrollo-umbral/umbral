namespace umbral_backend.Domain.Exceptions;

public sealed class ParticipantAssignedToDifferentTeamException : Exception
{
    public ParticipantAssignedToDifferentTeamException(Guid participantId, Guid currentTeamId, Guid requestedTeamId)
        : base($"Participant '{participantId}' is assigned to team '{currentTeamId}' and cannot reconnect to '{requestedTeamId}'.")
    {
    }
}
