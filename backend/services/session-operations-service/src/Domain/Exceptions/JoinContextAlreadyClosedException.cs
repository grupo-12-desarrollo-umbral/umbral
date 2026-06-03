namespace umbral_backend.Domain.Exceptions;

public sealed class JoinContextAlreadyClosedException : Exception
{
    public JoinContextAlreadyClosedException(Guid joinContextId)
        : base($"Join context '{joinContextId}' is already closed.")
    {
    }
}
