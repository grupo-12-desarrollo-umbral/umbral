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

    /// <summary>
    /// Multiplier applied to <see cref="ScoreValue.BaseTargetScore"/> to derive a
    /// target's score from its mission's difficulty tier: Beginner=1, Intermediate=2,
    /// Advanced=3 (the 1-based position in <see cref="AllowedValues"/>).
    /// </summary>
    public int ScoreFactor
    {
        get
        {
            for (var i = 0; i < AllowedValues.Count; i++)
            {
                if (string.Equals(AllowedValues[i], Value, StringComparison.OrdinalIgnoreCase))
                {
                    return i + 1;
                }
            }

            throw new InvalidDifficultyValueException(Value);
        }
    }

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
