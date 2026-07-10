namespace umbral_backend.Domain.Exceptions;

// The selected option does not exist among the snapshotted options of the active trivia question.
public sealed class InvalidTriviaAnswerOptionException : DomainException
{
    public InvalidTriviaAnswerOptionException(int selectedOptionSequenceOrder)
        : base($"Option '{selectedOptionSequenceOrder}' is not a valid option for the active trivia question.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
