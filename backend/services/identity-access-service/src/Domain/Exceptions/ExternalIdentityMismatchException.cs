namespace umbral_backend.Domain.Exceptions;

public sealed class ExternalIdentityMismatchException : Exception
{
    public ExternalIdentityMismatchException(int userId)
        : base($"User '{userId}' cannot be synchronized with a different external identity.")
    {
    }
}
