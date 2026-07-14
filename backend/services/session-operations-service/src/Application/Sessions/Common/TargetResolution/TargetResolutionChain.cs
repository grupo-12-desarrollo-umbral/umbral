using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.Common.TargetResolution;

public sealed class TargetResolutionChain
{
    private readonly TargetResolutionLink? _head;

    public TargetResolutionChain(IEnumerable<TargetResolutionLink> links)
    {
        TargetResolutionLink? head = null;
        TargetResolutionLink? previous = null;
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

    public Task<TargetResolutionRejectionReason?> ValidateAsync(
        TargetResolutionContext context,
        CancellationToken cancellationToken) =>
        _head?.ValidateAsync(context, cancellationToken) ??
        Task.FromResult<TargetResolutionRejectionReason?>(null);
}
