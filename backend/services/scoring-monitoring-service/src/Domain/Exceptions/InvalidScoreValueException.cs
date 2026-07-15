namespace umbral_backend.Domain.Exceptions;

public sealed class InvalidScoreValueException : DomainException
{
    public InvalidScoreValueException(int attemptedValue)
        : base($"Score value '{attemptedValue}' is invalid.")
    {
        AttemptedValue = attemptedValue;
    }

    public int AttemptedValue { get; }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
