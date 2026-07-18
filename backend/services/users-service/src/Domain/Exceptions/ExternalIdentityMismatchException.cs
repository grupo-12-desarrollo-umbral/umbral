namespace umbral_backend.Domain.Exceptions;

public sealed class ExternalIdentityMismatchException : DomainException
{
    public ExternalIdentityMismatchException(int userId)
        : base($"User '{userId}' cannot be synchronized with a different external identity.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
