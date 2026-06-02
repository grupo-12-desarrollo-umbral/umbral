namespace umbral_backend.Domain.Exceptions;

public sealed class QuestionTimerMustBePositiveException : Exception
{
    public QuestionTimerMustBePositiveException()
        : base("Trivia question timer must be positive.")
    {
    }
}
