using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class SubstagePlayModeMismatchException : DomainException
{
    public SubstagePlayModeMismatchException(SubstagePlayMode expected, SubstagePlayMode actual)
        : base($"This operation requires a {expected} substage but the substage is {actual}.")
    {
        Expected = expected;
        Actual = actual;
    }

    public SubstagePlayMode Expected { get; }

    public SubstagePlayMode Actual { get; }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
