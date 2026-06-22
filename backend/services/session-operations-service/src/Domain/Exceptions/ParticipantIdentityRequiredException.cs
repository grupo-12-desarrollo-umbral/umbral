namespace umbral_backend.Domain.Exceptions;

public sealed class ParticipantIdentityRequiredException : DomainException
{
    public ParticipantIdentityRequiredException()
        : base("Participant identity is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
