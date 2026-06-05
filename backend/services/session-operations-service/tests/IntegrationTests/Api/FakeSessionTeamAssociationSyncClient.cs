using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

public sealed class FakeSessionTeamAssociationSyncClient : ISessionTeamAssociationSyncClient
{
    private readonly List<(Guid LiveSessionId, string SessionCode, Guid TeamId)> _syncedAssociations = new();

    public IReadOnlyList<(Guid LiveSessionId, string SessionCode, Guid TeamId)> SyncedAssociations
        => _syncedAssociations;

    public Task SyncAssociationAsync(
        Guid liveSessionId,
        string sessionCode,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        _syncedAssociations.Add((liveSessionId, sessionCode, teamId));
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _syncedAssociations.Clear();
    }
}
