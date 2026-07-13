using umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation;

namespace umbral_backend.Application.Sessions.Common.TriviaAnswerValidation;

/// <summary>
/// Composes the shared evidence-admission chain with trivia-specific links. The full order is runtime
/// participation -> session admits reception -> active substage present -> active question -> timer
/// window -> duplicate team answer, and the first rejecting link short-circuits everything after it.
/// </summary>
public sealed class TriviaAnswerValidationChain
{
    private readonly EvidenceIntakeValidationChain _evidenceIntakeValidationChain;
    private readonly TriviaAnswerValidationLink? _head;

    public TriviaAnswerValidationChain(
        EvidenceIntakeValidationChain evidenceIntakeValidationChain,
        IEnumerable<TriviaAnswerValidationLink> links)
    {
        _evidenceIntakeValidationChain = evidenceIntakeValidationChain;
        TriviaAnswerValidationLink? head = null;
        TriviaAnswerValidationLink? previous = null;

        foreach (var link in links)
        {
            if (head is null)
            {
                head = link;
            }
            else
            {
                previous!.SetNext(link);
            }

            previous = link;
        }

        _head = head;
    }

    public async Task ValidateAsync(
        TriviaAnswerValidationContext context,
        CancellationToken cancellationToken)
    {
        await _evidenceIntakeValidationChain.ValidateAsync(
            new EvidenceIntakeValidationContext(
                context.Session,
                context.TeamId,
                context.TriviaSubstageSnapshotId,
                context.Token,
                context.SubmittedAt),
            cancellationToken);

        await ValidateConcreteFormAsync(context, cancellationToken);
    }

    public Task ValidateConcreteFormAsync(
        TriviaAnswerValidationContext context,
        CancellationToken cancellationToken)
    {
        return _head?.ValidateAsync(context, cancellationToken) ?? Task.CompletedTask;
    }
}
