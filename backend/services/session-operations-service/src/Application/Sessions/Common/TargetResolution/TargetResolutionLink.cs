using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.Common.TargetResolution;

public abstract class TargetResolutionLink
{
    private TargetResolutionLink? _next;

    public TargetResolutionLink SetNext(TargetResolutionLink next)
    {
        _next = next;
        return next;
    }

    public async Task<TargetResolutionRejectionReason?> ValidateAsync(
        TargetResolutionContext context,
        CancellationToken cancellationToken)
    {
        var rejection = await CheckAsync(context, cancellationToken);
        if (rejection is not null || _next is null)
        {
            return rejection;
        }

        return await _next.ValidateAsync(context, cancellationToken);
    }

    protected abstract Task<TargetResolutionRejectionReason?> CheckAsync(
        TargetResolutionContext context,
        CancellationToken cancellationToken);
}
