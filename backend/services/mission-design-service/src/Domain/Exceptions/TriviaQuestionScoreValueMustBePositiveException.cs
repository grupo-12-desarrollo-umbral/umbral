namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionScoreValueMustBePositiveException : Exception
{
    public TriviaQuestionScoreValueMustBePositiveException()
        : base("Trivia question score value must be positive.")
    {
    }
}
