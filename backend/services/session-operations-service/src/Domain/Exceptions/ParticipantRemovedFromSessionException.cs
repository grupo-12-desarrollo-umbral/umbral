namespace umbral_backend.Domain.Exceptions;

public sealed class ParticipantRemovedFromSessionException : DomainException
{
    public ParticipantRemovedFromSessionException(Guid participantId)
        : base($"Participant '{participantId}' was removed from the live session.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
