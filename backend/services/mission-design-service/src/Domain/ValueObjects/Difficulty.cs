using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

public sealed class Difficulty : ValueObject
{
    public static readonly IReadOnlyList<string> AllowedValues =
    [
        "Beginner",
        "Intermediate",
        "Advanced",
    ];

    private Difficulty()
    {
        Value = string.Empty;
    }

    private Difficulty(string value)
    {
        Value = value;
    }

    public string Value { get; private set; }

    public static Difficulty Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DifficultyValueRequiredException();
        }

        var trimmed = value.Trim();

        if (!AllowedValues.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDifficultyValueException(trimmed);
        }

        return new Difficulty(trimmed);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
