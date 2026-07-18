namespace umbral_backend.Domain.Exceptions;

public sealed class InvalidDifficultyFactorException : DomainException
{
    public InvalidDifficultyFactorException(int attemptedValue)
        : base($"Difficulty factor '{attemptedValue}' is invalid; it must be a positive integer.")
    {
        AttemptedValue = attemptedValue;
    }

    public int AttemptedValue { get; }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
