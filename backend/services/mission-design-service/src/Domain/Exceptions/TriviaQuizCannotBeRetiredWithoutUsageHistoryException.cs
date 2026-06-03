namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuizCannotBeRetiredWithoutUsageHistoryException : Exception
{
    public TriviaQuizCannotBeRetiredWithoutUsageHistoryException()
        : base("Only trivia quizzes that have already been used in a session can be retired from future use.")
    {
    }
}
