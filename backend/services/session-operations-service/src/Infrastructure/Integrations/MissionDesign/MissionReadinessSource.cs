using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Infrastructure.Integrations.MissionDesign;

/// <summary>
/// HTTP adapter for the mission-readiness read contract exposed by
/// mission-design-service (<c>GET /api/missions/{id}/readiness</c>). Transport
/// mapping only; the session-creation rules stay in <c>SessionCreationPolicy</c>.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class MissionReadinessSource : IMissionReadinessSource
{
    private const string InactiveActivationState = "Inactive";

    private readonly HttpClient _httpClient;
    private readonly ICurrentUser _currentUser;

    public MissionReadinessSource(HttpClient httpClient, ICurrentUser currentUser)
    {
        _httpClient = httpClient;
        _currentUser = currentUser;
    }

    public async Task<MissionReadinessDto?> GetByIdAsync(int missionId, CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/missions/{missionId}/readiness");
        ForwardTrustedHeaders(requestMessage);

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var readiness = await response.Content.ReadFromJsonAsync<MissionReadinessResponse>(cancellationToken);
        return readiness?.ToMissionReadinessDto();
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

    private sealed record MissionReadinessResponse(
        int MissionId,
        string ActivationState,
        bool IsReady,
        IReadOnlyList<string>? Failures)
    {
        public MissionReadinessDto ToMissionReadinessDto()
        {
            var isActive = !string.Equals(ActivationState, InactiveActivationState, StringComparison.OrdinalIgnoreCase);

            return new MissionReadinessDto(
                MissionId,
                ActivationState,
                isActive,
                IsReady,
                Failures ?? []);
        }
    }
}
