using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

public sealed class QuestionTimer : ValueObject
{
    internal const int MinimumSeconds = 15;
    internal const int MaximumSeconds = 30;

    private QuestionTimer()
    {
    }

    private QuestionTimer(int seconds)
    {
        Seconds = seconds;
    }

    public int Seconds { get; private set; }

    public static QuestionTimer Create(int seconds)
    {
        if (seconds < MinimumSeconds)
        {
            throw new QuestionTimerMustBePositiveException();
        }

        if (seconds > MaximumSeconds)
        {
            throw new QuestionTimerExceedsMaximumException();
        }

        return new QuestionTimer(seconds);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Seconds;
    }
}
