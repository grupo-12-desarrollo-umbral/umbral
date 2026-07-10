namespace umbral_backend.Domain.Exceptions;

// Trivia attribution invariant (HU-34): an accepted trivia answer must always be attributable to a
// session participant. Thrown when the authenticated caller cannot be resolved to a participant of
// this session — an absent/unparseable identity claim, or an authenticated non-participant holding a
// valid team token. Forbidden, not NotFound: the caller is authenticated but is not permitted to
// attribute an answer in a session they never joined.
public sealed class AnswerSubmitterIsNotSessionParticipantException : DomainException
{
    public AnswerSubmitterIsNotSessionParticipantException()
        : base("The trivia answer submitter must be a participant of this session.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Forbidden;
}
