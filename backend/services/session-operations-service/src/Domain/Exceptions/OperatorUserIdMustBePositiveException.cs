namespace umbral_backend.Domain.Exceptions;

public sealed class OperatorUserIdMustBePositiveException : Exception
{
    public OperatorUserIdMustBePositiveException()
        : base("Assigned operator user id must be greater than zero.")
    {
    }
}
