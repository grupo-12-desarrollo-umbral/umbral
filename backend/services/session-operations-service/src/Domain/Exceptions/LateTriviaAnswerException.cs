namespace umbral_backend.Domain.Exceptions;

// The active question's timer window has elapsed: the answer arrived after the window closed and is
// rejected as late (first-write-wins keys off this same question window, not a wall clock).
public sealed class LateTriviaAnswerException : DomainException
{
    public LateTriviaAnswerException()
        : base("The trivia answer arrived after the active question's timer window closed.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
