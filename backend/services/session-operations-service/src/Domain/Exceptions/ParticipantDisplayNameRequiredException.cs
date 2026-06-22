namespace umbral_backend.Domain.Exceptions;

public sealed class ParticipantDisplayNameRequiredException : DomainException
{
    public ParticipantDisplayNameRequiredException()
        : base("Participant display name is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
