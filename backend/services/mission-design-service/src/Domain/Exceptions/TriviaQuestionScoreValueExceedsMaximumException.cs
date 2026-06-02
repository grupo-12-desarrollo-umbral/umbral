namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionScoreValueExceedsMaximumException : Exception
{
    public TriviaQuestionScoreValueExceedsMaximumException()
        : base("Trivia question score value cannot exceed 100.")
    {
    }
}
