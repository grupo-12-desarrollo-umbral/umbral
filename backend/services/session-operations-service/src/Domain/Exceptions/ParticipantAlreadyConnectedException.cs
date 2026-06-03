namespace umbral_backend.Domain.Exceptions;

public sealed class ParticipantAlreadyConnectedException : Exception
{
    public ParticipantAlreadyConnectedException(Guid participantId)
        : base($"Participant '{participantId}' is already connected.")
    {
    }
}
