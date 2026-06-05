namespace umbral_backend.Application.Common.Interfaces;

public interface ISessionTeamAssociationSyncClient
{
    Task SyncAssociationAsync(
        Guid liveSessionId,
        string sessionCode,
        Guid teamId,
        CancellationToken cancellationToken);
}
