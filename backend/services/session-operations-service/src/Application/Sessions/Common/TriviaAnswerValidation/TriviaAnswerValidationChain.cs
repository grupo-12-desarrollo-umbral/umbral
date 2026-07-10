namespace umbral_backend.Application.Sessions.Common.TriviaAnswerValidation;

/// <summary>
/// Builds the ordered Chain of Responsibility from the registered links and runs it. Order follows
/// DI registration order (runtime participation -> active question -> timer window -> duplicate team
/// answer), so a new link is added by registering it in <c>DependencyInjection</c> without touching
/// this pipeline. Running the chain short-circuits at the first rejecting link.
/// </summary>
public sealed class TriviaAnswerValidationChain
{
    private readonly TriviaAnswerValidationLink? _head;

    public TriviaAnswerValidationChain(IEnumerable<TriviaAnswerValidationLink> links)
    {
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

    public Task ValidateAsync(TriviaAnswerValidationContext context, CancellationToken cancellationToken)
    {
        return _head?.ValidateAsync(context, cancellationToken) ?? Task.CompletedTask;
    }
}
