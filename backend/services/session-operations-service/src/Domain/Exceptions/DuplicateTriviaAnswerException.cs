namespace umbral_backend.Domain.Exceptions;

// First-write-wins rejection: the team already holds an accepted answer for this snapshotted trivia
// question, so a repeat attempt is rejected.
public sealed class DuplicateTriviaAnswerException : DomainException
{
    public DuplicateTriviaAnswerException(Guid teamId, int questionSequenceOrder)
        : base($"Team '{teamId}' has already answered trivia question '{questionSequenceOrder}' in this session.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
