namespace umbral_backend.Domain.Exceptions;

public sealed class InvalidDifficultyValueException : Exception
{
    public InvalidDifficultyValueException(string value)
        : base($"'{value}' is not a valid difficulty value. Allowed values: {string.Join(", ", ValueObjects.Difficulty.AllowedValues)}")
    {
    }
}
