namespace umbral_backend.Application.Sessions.Common.EvidenceValidation;

/// <summary>
/// Builds the contextual EvidenceValidationPolicy chain in DI registration order.
/// </summary>
public sealed class EvidenceValidationChain
{
    private readonly EvidenceValidationLink? _head;

    public EvidenceValidationChain(IEnumerable<EvidenceValidationLink> links)
    {
        EvidenceValidationLink? head = null;
        EvidenceValidationLink? previous = null;

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

    public Task ValidateAsync(
        EvidenceValidationContext context,
        CancellationToken cancellationToken)
    {
        return _head?.ValidateAsync(context, cancellationToken) ?? Task.CompletedTask;
    }
}
