namespace umbral_backend.Domain.Exceptions;

public sealed class ExternalIdentityIdRequiredException : DomainException
{
    public ExternalIdentityIdRequiredException()
        : base("External identity id is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
