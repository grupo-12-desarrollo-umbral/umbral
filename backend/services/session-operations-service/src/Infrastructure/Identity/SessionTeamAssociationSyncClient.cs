using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Infrastructure.Identity;

[ExcludeFromCodeCoverage]
public sealed class SessionTeamAssociationSyncClient : ISessionTeamAssociationSyncClient
{
    private readonly HttpClient _httpClient;
    private readonly ICurrentUser _currentUser;

    public SessionTeamAssociationSyncClient(HttpClient httpClient, ICurrentUser currentUser)
    {
        _httpClient = httpClient;
        _currentUser = currentUser;
    }

    public async Task SyncAssociationAsync(
        Guid liveSessionId,
        string sessionCode,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/sessions/{sessionCode}/teams")
        {
            Content = JsonContent.Create(new SyncAssociationRequest(liveSessionId, teamId))
        };
        ForwardTrustedHeaders(requestMessage);

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

        // Identity-access treats an already-associated team as idempotent success (409).
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
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

    private sealed record SyncAssociationRequest(Guid LiveSessionId, Guid TeamId);
}
