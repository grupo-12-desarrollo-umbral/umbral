namespace umbral_backend.Domain.Exceptions;

public sealed class JoinContextAlreadyClosedException : DomainException
{
    public JoinContextAlreadyClosedException(Guid joinContextId)
        : base($"Join context '{joinContextId}' is already closed.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
