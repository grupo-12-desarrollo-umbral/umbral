using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

public sealed class MaximumTime : ValueObject
{
    public const int MaximumMinutes = 30;

    private MaximumTime()
    {
    }

    private MaximumTime(int minutes)
    {
        Minutes = minutes;
    }

    public int Minutes { get; private set; }

    public static MaximumTime Create(int minutes)
    {
        if (minutes <= 0)
        {
            throw new MaximumTimeMustBePositiveException();
        }

        if (minutes > MaximumMinutes)
        {
            throw new MaximumTimeExceedsLimitException(MaximumMinutes);
        }

        return new MaximumTime(minutes);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Minutes;
    }
}
