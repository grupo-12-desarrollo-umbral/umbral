namespace umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation;

/// <summary>
/// Builds the generic evidence-admission chain in DI registration order: runtime participation,
/// session admission, then active-substage presence.
/// </summary>
public sealed class EvidenceIntakeValidationChain
{
    private readonly EvidenceIntakeValidationLink? _head;

    public EvidenceIntakeValidationChain(IEnumerable<EvidenceIntakeValidationLink> links)
    {
        EvidenceIntakeValidationLink? head = null;
        EvidenceIntakeValidationLink? previous = null;

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
        EvidenceIntakeValidationContext context,
        CancellationToken cancellationToken)
    {
        return _head?.ValidateAsync(context, cancellationToken) ?? Task.CompletedTask;
    }
}
