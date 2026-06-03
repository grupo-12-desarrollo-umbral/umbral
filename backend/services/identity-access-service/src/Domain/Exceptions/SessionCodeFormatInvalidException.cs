namespace umbral_backend.Domain.Exceptions;

public sealed class SessionCodeFormatInvalidException : Exception
{
    public SessionCodeFormatInvalidException(int requiredLength)
        : base($"Session code must be exactly {requiredLength} alphanumeric characters.")
    {
    }
}
