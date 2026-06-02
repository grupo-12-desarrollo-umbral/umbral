namespace umbral_backend.Domain.Exceptions;

public sealed class QuestionTimerExceedsMaximumException : Exception
{
    public QuestionTimerExceedsMaximumException()
        : base("Trivia question timer cannot exceed 120 seconds.")
    {
    }
}
