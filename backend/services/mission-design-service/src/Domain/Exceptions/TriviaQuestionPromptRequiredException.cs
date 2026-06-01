namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionPromptRequiredException : Exception
{
    public TriviaQuestionPromptRequiredException()
        : base("Trivia question prompt is required.")
    {
    }
}
