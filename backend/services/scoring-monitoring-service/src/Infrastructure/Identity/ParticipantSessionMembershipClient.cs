using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Infrastructure.Identity;

[ExcludeFromCodeCoverage]
public sealed class ParticipantSessionMembershipClient : IParticipantSessionMembershipClient
{
    private const string RequestUriTemplate = "/api/sessions/{0}/participants/session-membership";
    private const string UnavailableReasonCode = "session-ops-unavailable";

    private readonly HttpClient _httpClient;
    private readonly ICurrentUser _currentUser;

    public ParticipantSessionMembershipClient(HttpClient httpClient, ICurrentUser currentUser)
    {
        _httpClient = httpClient;
        _currentUser = currentUser;
    }

    public async Task<ParticipantSessionMembershipDecisionDto> ValidateAsync(
        Guid liveSessionId,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        try
        {
            var requestUri = string.Format(RequestUriTemplate, liveSessionId)
                + $"?teamId={teamId}";

            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
            ForwardTrustedHeaders(requestMessage);

            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Deny(liveSessionId, teamId, $"session-ops returned HTTP {(int)response.StatusCode}.");
            }

            var decision = await response.Content.ReadFromJsonAsync<ParticipantSessionMembershipDecisionDto>(cancellationToken);
            return decision ?? Deny(liveSessionId, teamId, "session-ops returned an empty membership decision.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Deny(liveSessionId, teamId, "session-ops membership check timed out.");
        }
        catch (HttpRequestException)
        {
            return Deny(liveSessionId, teamId, "session-ops membership check is unavailable.");
        }
        catch (JsonException)
        {
            return Deny(liveSessionId, teamId, "session-ops returned an unreadable membership decision.");
        }
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

    private static ParticipantSessionMembershipDecisionDto Deny(Guid liveSessionId, Guid teamId, string reason)
    {
        return new ParticipantSessionMembershipDecisionDto(false, liveSessionId, teamId, UnavailableReasonCode);
    }
}
