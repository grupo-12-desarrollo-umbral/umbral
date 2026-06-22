namespace umbral_backend.Domain.Exceptions;

public sealed class QuestionAlreadyActiveException : DomainException
{
    public QuestionAlreadyActiveException(int activeQuestionIndex)
        : base($"Question index '{activeQuestionIndex}' is already active.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
