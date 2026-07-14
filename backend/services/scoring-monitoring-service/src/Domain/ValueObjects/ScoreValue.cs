using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

public sealed class ScoreValue : ValueObject
{
    private ScoreValue(int value)
    {
        Value = value;
    }

    public int Value { get; }

    public static ScoreValue Create(int value)
    {
        if (value < 0)
        {
            throw new InvalidScoreValueException(value);
        }

        return new ScoreValue(value);
    }

    public static ScoreValue Zero() => new(0);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
