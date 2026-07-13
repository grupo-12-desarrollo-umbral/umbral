namespace umbral_backend.Application.Sessions.Common.TriviaAnswerValidation;

/// <summary>
/// Chain of Responsibility link guarding a single trivia-answer acceptance concern. Each link runs
/// its own <see cref="CheckAsync"/> and, only on success, delegates to the next link. The first
/// failing link throws its typed rejection and short-circuits the rest of the chain. These links are
/// the trivia-specific extension after the shared evidence-admission chain: active question -> timer
/// window -> duplicate team answer.
/// </summary>
public abstract class TriviaAnswerValidationLink
{
    private TriviaAnswerValidationLink? _next;

    public TriviaAnswerValidationLink SetNext(TriviaAnswerValidationLink next)
    {
        _next = next;
        return next;
    }

    public async Task ValidateAsync(TriviaAnswerValidationContext context, CancellationToken cancellationToken)
    {
        await CheckAsync(context, cancellationToken);

        if (_next is not null)
        {
            await _next.ValidateAsync(context, cancellationToken);
        }
    }

    protected abstract Task CheckAsync(TriviaAnswerValidationContext context, CancellationToken cancellationToken);
}
