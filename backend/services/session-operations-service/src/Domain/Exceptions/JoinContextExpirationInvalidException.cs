namespace umbral_backend.Domain.Exceptions;

public sealed class JoinContextExpirationInvalidException : DomainException
{
    public JoinContextExpirationInvalidException()
        : base("Join context expiration must be after creation time.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
