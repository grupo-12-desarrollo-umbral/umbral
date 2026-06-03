namespace umbral_backend.Application.Common.Exceptions;

public sealed class SourceTriviaQuizNotPublishedException : Exception
{
    public SourceTriviaQuizNotPublishedException(int triviaQuizId, string status)
        : base($"Trivia quiz \"{triviaQuizId}\" is not published. Current status: {status}.")
    {
        TriviaQuizId = triviaQuizId;
        Status = status;
    }

    public int TriviaQuizId { get; }

    public string Status { get; }
}
