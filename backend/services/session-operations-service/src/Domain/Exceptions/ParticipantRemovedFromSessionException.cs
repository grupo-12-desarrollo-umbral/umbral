namespace umbral_backend.Domain.Exceptions;

public sealed class ParticipantRemovedFromSessionException : Exception
{
    public ParticipantRemovedFromSessionException(Guid participantId)
        : base($"Participant '{participantId}' was removed from the live session.")
    {
    }
}
