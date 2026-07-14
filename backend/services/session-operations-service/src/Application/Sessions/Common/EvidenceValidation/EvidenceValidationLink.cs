namespace umbral_backend.Application.Sessions.Common.EvidenceValidation;

/// <summary>
/// One independently testable contextual check in the EvidenceValidationPolicy Chain of
/// Responsibility. A link delegates only after its own check succeeds.
/// </summary>
public abstract class EvidenceValidationLink
{
    private EvidenceValidationLink? _next;

    public EvidenceValidationLink SetNext(EvidenceValidationLink next)
    {
        _next = next;
        return next;
    }

    public async Task ValidateAsync(
        EvidenceValidationContext context,
        CancellationToken cancellationToken)
    {
        await CheckAsync(context, cancellationToken);

        if (_next is not null)
        {
            await _next.ValidateAsync(context, cancellationToken);
        }
    }

    protected abstract Task CheckAsync(
        EvidenceValidationContext context,
        CancellationToken cancellationToken);
}
