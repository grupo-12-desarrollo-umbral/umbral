using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Common.TriviaAnswerValidation;

// Immutable input the ordered trivia-answer links read (never mutate). Carries only what the four
// links need: the loaded aggregate, the answering team id (runtime or reference), the declared
// snapshotted-question composite key (substage snapshot id + question sequence order — the frozen
// snapshot has no per-question Guid), the runtime participation token, and the single submission
// timestamp shared by the window link and the domain skeleton so the "in time" decision is evaluated
// against one clock reading.
public sealed class TriviaAnswerValidationContext
{
    public TriviaAnswerValidationContext(
        LiveSession session,
        Guid teamId,
        Guid triviaSubstageSnapshotId,
        int questionSequenceOrder,
        string? token,
        DateTimeOffset submittedAt)
    {
        Session = session;
        TeamId = teamId;
        TriviaSubstageSnapshotId = triviaSubstageSnapshotId;
        QuestionSequenceOrder = questionSequenceOrder;
        Token = token;
        SubmittedAt = submittedAt;
    }

    public LiveSession Session { get; }

    public Guid TeamId { get; }

    public Guid TriviaSubstageSnapshotId { get; }

    public int QuestionSequenceOrder { get; }

    public string? Token { get; }

    public DateTimeOffset SubmittedAt { get; }
}
