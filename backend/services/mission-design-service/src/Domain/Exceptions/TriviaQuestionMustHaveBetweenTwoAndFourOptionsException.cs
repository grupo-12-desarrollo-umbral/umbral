namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionMustHaveBetweenTwoAndFourOptionsException : Exception
{
    public TriviaQuestionMustHaveBetweenTwoAndFourOptionsException()
        : base("Trivia question must define between two and four options.")
    {
    }
}
