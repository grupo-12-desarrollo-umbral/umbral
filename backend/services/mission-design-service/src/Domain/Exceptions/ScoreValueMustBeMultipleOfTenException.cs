using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Exceptions;

public sealed class ScoreValueMustBeMultipleOfTenException : DomainException
{
    public ScoreValueMustBeMultipleOfTenException()
        : base($"Score value must be a multiple of {ScoreValue.PointsIncrement} points.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
