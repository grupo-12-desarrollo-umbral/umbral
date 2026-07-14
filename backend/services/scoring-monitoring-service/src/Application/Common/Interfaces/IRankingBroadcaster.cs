namespace umbral_backend.Application.Common.Interfaces;

public interface IRankingBroadcaster
{
    Task RankingChanged(Guid liveSessionId, RankingSnapshotDto snapshot, CancellationToken cancellationToken);
}
