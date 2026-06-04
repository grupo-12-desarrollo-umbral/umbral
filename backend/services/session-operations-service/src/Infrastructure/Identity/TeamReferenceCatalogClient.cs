using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Infrastructure.Identity;

[ExcludeFromCodeCoverage]
public sealed class TeamReferenceCatalogClient : ITeamReferenceCatalogClient
{
    public async Task<TeamReferenceDto?> GetByIdAsync(Guid teamId, CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/teams/{teamId}");
        ForwardTrustedHeaders(requestMessage);

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

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
    private readonly ICurrentUser _currentUser;

    public TeamReferenceCatalogClient(HttpClient httpClient, ICurrentUser currentUser)
    {
        _httpClient = httpClient;
        _currentUser = currentUser;
    }

    private void ForwardTrustedHeaders(HttpRequestMessage requestMessage)
    {
        AddHeaderIfPresent(requestMessage, "X-User-Id", _currentUser.Id);
        AddHeaderIfPresent(requestMessage, "X-User-Role", _currentUser.Role);
        AddHeaderIfPresent(requestMessage, "X-User-Email", _currentUser.Email);
    }

    private static void AddHeaderIfPresent(HttpRequestMessage requestMessage, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            requestMessage.Headers.TryAddWithoutValidation(name, value);
        }
    }

    private sealed record TeamReferenceCatalogResponse(
        Guid TeamId,
        string DisplayName,
        string TeamCode,
        bool IsActive,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
