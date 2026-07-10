using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

public sealed class ScoreValue : ValueObject
{
    public const int MaximumPoints = 150;

    // A target's score is not chosen by operators: it is derived as
    // BaseTargetScore * Difficulty.ScoreFactor (50 * {1,2,3} => 50 / 100 / 150),
    // so every score is a multiple of PointsIncrement within [Increment, Maximum].
    public const int BaseTargetScore = 50;

    public const int PointsIncrement = 10;

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

        if (points % PointsIncrement != 0)
        {
            throw new ScoreValueMustBeMultipleOfTenException();
        }

        return new ScoreValue(points);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Points;
    }
}
