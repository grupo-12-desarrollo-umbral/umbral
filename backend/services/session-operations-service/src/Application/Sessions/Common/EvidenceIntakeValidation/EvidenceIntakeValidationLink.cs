namespace umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation;

/// <summary>
/// One independently testable admission check in the shared evidence-intake Chain of Responsibility.
/// A link delegates only after its own check succeeds, so the first rejection short-circuits the chain.
/// </summary>
public abstract class EvidenceIntakeValidationLink
{
    private EvidenceIntakeValidationLink? _next;

    public EvidenceIntakeValidationLink SetNext(EvidenceIntakeValidationLink next)
    {
        _next = next;
        return next;
    }

    public async Task ValidateAsync(
        EvidenceIntakeValidationContext context,
        CancellationToken cancellationToken)
    {
        await CheckAsync(context, cancellationToken);

        if (_next is not null)
        {
            await _next.ValidateAsync(context, cancellationToken);
        }
    }

    protected abstract Task CheckAsync(
        EvidenceIntakeValidationContext context,
        CancellationToken cancellationToken);
}
