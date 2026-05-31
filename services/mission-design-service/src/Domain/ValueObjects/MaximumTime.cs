using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

public sealed class MaximumTime : ValueObject
{
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

        return new MaximumTime(minutes);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Minutes;
    }
}
