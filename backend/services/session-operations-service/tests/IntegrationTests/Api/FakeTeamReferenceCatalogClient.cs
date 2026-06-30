using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

public sealed class FakeTeamReferenceCatalogClient : ITeamReferenceCatalogClient
{
    private readonly Dictionary<Guid, TeamReferenceDto> _teams = new();

    public Task<TeamReferenceDto?> GetByIdAsync(Guid teamId, CancellationToken cancellationToken)
    {
        _teams.TryGetValue(teamId, out var team);
        return Task.FromResult(team);
    }

    public void Seed(TeamReferenceDto team)
    {
        _teams[team.TeamId] = team;
    }

    public void Reset()
    {
        _teams.Clear();
    }
}
