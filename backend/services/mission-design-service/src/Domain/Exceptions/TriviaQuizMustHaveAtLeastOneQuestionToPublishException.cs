namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuizMustHaveAtLeastOneQuestionToPublishException : Exception
{
    public TriviaQuizMustHaveAtLeastOneQuestionToPublishException()
        : base("Trivia quiz must contain at least one question before publication.")
    {
    }
}
