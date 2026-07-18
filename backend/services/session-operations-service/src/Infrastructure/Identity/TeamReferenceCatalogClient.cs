using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Infrastructure.Identity;

[ExcludeFromCodeCoverage]
public sealed class TeamReferenceCatalogClient : ITeamReferenceCatalogClient
{
    private readonly HttpClient _httpClient;
    private readonly ICurrentUser _currentUser;

    public TeamReferenceCatalogClient(HttpClient httpClient, ICurrentUser currentUser)
    {
        _httpClient = httpClient;
        _currentUser = currentUser;
    }

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

        var team = await response.Content.ReadFromJsonAsync<TeamReferenceCatalogResponse>(cancellationToken)
            ?? throw new InvalidOperationException(
                $"Users-service returned an empty team reference payload for team '{teamId}'.");

        var participantCount = await GetParticipantCountAsync(teamId, cancellationToken);

        return new TeamReferenceDto(
            team.TeamId,
            team.DisplayName,
            team.TeamCode,
            team.IsActive,
            participantCount);
    }

    private async Task<int> GetParticipantCountAsync(Guid teamId, CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/teams/{teamId}/participants");
        ForwardTrustedHeaders(requestMessage);

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        response.EnsureSuccessStatusCode();

        var participants = await response.Content.ReadFromJsonAsync<IReadOnlyList<TeamParticipantResponse>>(cancellationToken)
            ?? throw new InvalidOperationException(
                $"Users-service returned an empty team participant payload for team '{teamId}'.");

        return participants.Count;
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

    private sealed record TeamParticipantResponse(
        Guid TeamMembershipId,
        Guid TeamId,
        int UserId,
        string Email,
        string DisplayName);
}
