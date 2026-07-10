namespace umbral_backend.Domain.Exceptions;

// The active substage is not a trivia substage (e.g. a parked treasure-hunt substage), so a trivia
// answer cannot be registered against it.
public sealed class TriviaAnswerRequiresTriviaSubstageException : DomainException
{
    public TriviaAnswerRequiresTriviaSubstageException()
        : base("Registering a trivia answer requires the active substage to be a trivia substage.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
