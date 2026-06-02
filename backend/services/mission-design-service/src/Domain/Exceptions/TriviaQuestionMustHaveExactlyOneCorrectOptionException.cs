namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionMustHaveExactlyOneCorrectOptionException : Exception
{
    public TriviaQuestionMustHaveExactlyOneCorrectOptionException()
        : base("Trivia question must define exactly one correct option.")
    {
    }
}
