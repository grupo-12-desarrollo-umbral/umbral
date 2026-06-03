using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Infrastructure.Identity;

/// <summary>
/// HTTP adapter that resolves the participant membership access fact from the
/// identity-access-service. External integration glue — excluded from coverage.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ParticipantMembershipAccessClient : IParticipantMembershipAccessClient
{
    private const string RequestUri = "/api/permissions/participant-membership-access";

    private readonly HttpClient _httpClient;
    private readonly ICurrentUser _currentUser;

    public ParticipantMembershipAccessClient(HttpClient httpClient, ICurrentUser currentUser)
    {
        _httpClient = httpClient;
        _currentUser = currentUser;
    }

    public async Task<ParticipantMembershipAccessDecisionDto> ValidateAsync(
        Guid liveSessionId,
        Guid teamId,
        string? token,
        CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, RequestUri)
        {
            Content = JsonContent.Create(new ParticipantMembershipAccessRequest(liveSessionId, teamId, token))
        };

        ForwardTrustedHeaders(requestMessage);

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        response.EnsureSuccessStatusCode();

        var decision = await response.Content.ReadFromJsonAsync<ParticipantMembershipAccessDecisionDto>(cancellationToken);

        return decision ?? throw new InvalidOperationException(
            "Identity-access-service returned an empty participant membership access decision.");
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

    private sealed record ParticipantMembershipAccessRequest(Guid LiveSessionId, Guid TeamId, string? Token);
}
