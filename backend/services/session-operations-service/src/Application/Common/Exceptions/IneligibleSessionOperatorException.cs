namespace umbral_backend.Application.Common.Exceptions;

public sealed class IneligibleSessionOperatorException : Exception
{
    public IneligibleSessionOperatorException(int operatorUserId)
        : base($"User '{operatorUserId}' is not eligible to be assigned as a session operator.")
    {
        OperatorUserId = operatorUserId;
    }

    public int OperatorUserId { get; }
}
