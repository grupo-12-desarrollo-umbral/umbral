namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionSequenceOrderMustBePositiveException : DomainException
{
    public TriviaQuestionSequenceOrderMustBePositiveException()
        : base("Trivia question sequence order must be a positive integer.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
