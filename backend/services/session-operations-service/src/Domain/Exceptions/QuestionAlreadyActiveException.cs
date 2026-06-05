namespace umbral_backend.Domain.Exceptions;

public sealed class QuestionAlreadyActiveException : Exception
{
    public QuestionAlreadyActiveException(int activeQuestionIndex)
        : base($"Question index '{activeQuestionIndex}' is already active.")
    {
    }
}
