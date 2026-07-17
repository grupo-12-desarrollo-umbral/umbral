using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Sessions.Common.TargetResolution;

public sealed class TargetResolutionContext
{
    public TargetResolutionContext(
        LiveSession session,
        Guid teamId,
        Guid activeSubstageId,
        string scannedValue)
    {
        Session = session;
        TeamId = teamId;
        ActiveSubstageId = activeSubstageId;
        ScannedValue = scannedValue;

        // Authoring enforces mission-scoped QR uniqueness, but legacy missions snapshotted before
        // that guard can still hold duplicates. Counting the matches rather than demanding exactly
        // one keeps an ambiguous scan a defined rejection instead of an InvalidOperationException,
        // and never silently picks a winner.
        var matches = session.MissionRuntimeSnapshot.TargetSnapshots
            .Where(target => string.Equals(target.QrCode, scannedValue.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        MatchedTargetCount = matches.Count;
        ResolvedTarget = matches.Count == 1 ? matches[0] : null;
    }

    public LiveSession Session { get; }
    public Guid TeamId { get; }
    public Guid ActiveSubstageId { get; }
    public string ScannedValue { get; }
    public TargetSnapshot? ResolvedTarget { get; }

    /// <summary>
    /// Number of snapshotted targets whose QR code matches the scanned value. Greater than one
    /// means the scan is ambiguous and <see cref="ResolvedTarget"/> is deliberately left null.
    /// </summary>
    public int MatchedTargetCount { get; }

    public bool IsAmbiguous => MatchedTargetCount > 1;
}
