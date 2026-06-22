using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Exceptions;

public sealed class ScoreValueExceedsMaximumException : DomainException
{
    public ScoreValueExceedsMaximumException()
        : base($"Score value must not exceed {ScoreValue.MaximumPoints} points.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
