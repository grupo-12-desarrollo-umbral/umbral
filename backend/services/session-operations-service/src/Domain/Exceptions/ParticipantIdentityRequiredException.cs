namespace umbral_backend.Domain.Exceptions;

public sealed class ParticipantIdentityRequiredException : Exception
{
    public ParticipantIdentityRequiredException()
        : base("Participant identity is required.")
    {
    }
}
