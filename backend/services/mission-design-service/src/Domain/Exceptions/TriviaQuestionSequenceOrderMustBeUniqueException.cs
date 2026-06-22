namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionSequenceOrderMustBeUniqueException : DomainException
{
    public TriviaQuestionSequenceOrderMustBeUniqueException(int sequenceOrder)
        : base($"Trivia question sequence order '{sequenceOrder}' is already in use within the quiz.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
