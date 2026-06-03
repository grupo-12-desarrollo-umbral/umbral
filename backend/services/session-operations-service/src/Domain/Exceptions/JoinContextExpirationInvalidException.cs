namespace umbral_backend.Domain.Exceptions;

public sealed class JoinContextExpirationInvalidException : Exception
{
    public JoinContextExpirationInvalidException()
        : base("Join context expiration must be after creation time.")
    {
    }
}
