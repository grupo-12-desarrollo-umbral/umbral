using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Infrastructure.Identity;

/// <summary>
/// HTTP adapter that resolves team reference data from the identity-access-service.
/// External integration glue — excluded from coverage.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class TeamReferenceCatalogClient : ITeamReferenceCatalogClient
{
    public async Task<TeamReferenceDto?> GetByIdAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync($"/api/teams/{teamId}", cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var team = await response.Content.ReadFromJsonAsync<TeamReferenceCatalogResponse>(cancellationToken);

        return team is null
            ? throw new InvalidOperationException(
                $"Identity-access-service returned an empty team reference payload for team '{teamId}'.")
            : new TeamReferenceDto(team.TeamId, team.DisplayName, team.TeamCode, team.IsActive);
    }

    private readonly HttpClient _httpClient;

    public TeamReferenceCatalogClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private sealed record TeamReferenceCatalogResponse(
        Guid TeamId,
        string DisplayName,
        string TeamCode,
        bool IsActive,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
