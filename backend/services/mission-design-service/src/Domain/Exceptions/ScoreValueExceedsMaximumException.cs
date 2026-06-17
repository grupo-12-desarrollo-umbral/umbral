using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Exceptions;

public sealed class ScoreValueExceedsMaximumException : Exception
{
    public ScoreValueExceedsMaximumException()
        : base($"Score value must not exceed {ScoreValue.MaximumPoints} points.")
    {
    }
}
