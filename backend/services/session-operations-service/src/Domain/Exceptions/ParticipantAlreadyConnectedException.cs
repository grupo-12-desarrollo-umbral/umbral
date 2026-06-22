namespace umbral_backend.Domain.Exceptions;

public sealed class ParticipantAlreadyConnectedException : DomainException
{
    public ParticipantAlreadyConnectedException(Guid participantId)
        : base($"Participant '{participantId}' is already connected.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
