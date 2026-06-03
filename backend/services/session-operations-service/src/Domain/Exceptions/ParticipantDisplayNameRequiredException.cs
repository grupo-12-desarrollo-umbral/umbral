namespace umbral_backend.Domain.Exceptions;

public sealed class ParticipantDisplayNameRequiredException : Exception
{
    public ParticipantDisplayNameRequiredException()
        : base("Participant display name is required.")
    {
    }
}
