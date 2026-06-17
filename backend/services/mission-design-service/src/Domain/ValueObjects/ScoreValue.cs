using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

public sealed class ScoreValue : ValueObject
{
    public const int MaximumPoints = 100;

    private ScoreValue()
    {
    }

    private ScoreValue(int points)
    {
        Points = points;
    }

    public int Points { get; private set; }

    public static ScoreValue Create(int points)
    {
        if (points <= 0)
        {
            throw new ScoreValueMustBePositiveException();
        }

        if (points > MaximumPoints)
        {
            throw new ScoreValueExceedsMaximumException();
        }

        return new ScoreValue(points);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Points;
    }
}
