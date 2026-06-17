using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class SubstagePlayModeMismatchException : Exception
{
    public SubstagePlayModeMismatchException(SubstagePlayMode expected, SubstagePlayMode actual)
        : base($"This operation requires a {expected} substage but the substage is {actual}.")
    {
        Expected = expected;
        Actual = actual;
    }

    public SubstagePlayMode Expected { get; }

    public SubstagePlayMode Actual { get; }
}
