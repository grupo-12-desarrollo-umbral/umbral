using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.Sessions.Common.TriviaAnswerValidation.Validators;

/// <summary>
/// Second trivia-specific link: the answer is in time only while the active question's authoritative timer window is
/// still open (HU-22). Keys off the same expiry the question-close path uses — the aggregate's own
/// timer snapshot evaluated at the shared submission instant — not a wall clock or client timer. Runs
/// after the active-question link, so a snapshot is only read once a live question exists.
/// </summary>
public sealed class TriviaAnswerWindowLink : TriviaAnswerValidationLink
{
    protected override Task CheckAsync(TriviaAnswerValidationContext context, CancellationToken cancellationToken)
    {
        var timerSnapshot = context.Session.GetActiveQuestionTimerSnapshot(context.SubmittedAt);

        if (timerSnapshot.IsExpired)
        {
            throw new LateTriviaAnswerException();
        }

        return Task.CompletedTask;
    }
}
