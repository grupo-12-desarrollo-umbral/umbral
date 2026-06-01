namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionSequenceOrderMustBePositiveException : Exception
{
    public TriviaQuestionSequenceOrderMustBePositiveException()
        : base("Trivia question sequence order must be a positive integer.")
    {
    }
}
