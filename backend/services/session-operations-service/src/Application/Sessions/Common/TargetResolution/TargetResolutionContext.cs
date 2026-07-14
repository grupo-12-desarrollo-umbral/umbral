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
        ResolvedTarget = session.MissionRuntimeSnapshot.TargetSnapshots.SingleOrDefault(target =>
            string.Equals(target.QrCode, scannedValue.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public LiveSession Session { get; }
    public Guid TeamId { get; }
    public Guid ActiveSubstageId { get; }
    public string ScannedValue { get; }
    public TargetSnapshot? ResolvedTarget { get; }
}
