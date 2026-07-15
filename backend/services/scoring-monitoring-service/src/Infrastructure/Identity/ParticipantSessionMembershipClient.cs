using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Infrastructure.Identity;

[ExcludeFromCodeCoverage]
public sealed class ParticipantSessionMembershipClient : IParticipantSessionMembershipClient
{
    private const string RequestUriTemplate = "/api/sessions/{0}/participants/session-membership";

    private readonly HttpClient _httpClient;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ParticipantSessionMembershipClient> _logger;

    public ParticipantSessionMembershipClient(
        HttpClient httpClient,
        ICurrentUser currentUser,
        ILogger<ParticipantSessionMembershipClient> logger)
    {
        _httpClient = httpClient;
        _currentUser = currentUser;
        _logger = logger;
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
                // Distinguishes a refusal (e.g. 403: session-ops only answers this for the Participant
                // role) from an outage. Collapsing both into "unavailable" sends debugging to the wrong
                // service — session-ops answering "no" looks identical to session-ops being down.
                return Deny(liveSessionId, teamId, $"session-ops-http-{(int)response.StatusCode}");
            }

            var decision = await response.Content.ReadFromJsonAsync<ParticipantSessionMembershipDecisionDto>(cancellationToken);
            return decision ?? Deny(liveSessionId, teamId, "session-ops-empty-decision");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Deny(liveSessionId, teamId, "session-ops-timeout");
        }
        catch (HttpRequestException)
        {
            return Deny(liveSessionId, teamId, "session-ops-unavailable");
        }
        catch (JsonException)
        {
            return Deny(liveSessionId, teamId, "session-ops-unreadable-decision");
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

    // Reached only when no membership answer could be obtained. A real decision (including a legitimate
    // "no") carries session-ops' own reason code and returns above. RankingSessionMembershipGuard drops
    // the decision and throws ForbiddenAccessException, so this log is the only surviving account of why
    // a caller was refused — keep the reason on both the DTO and the log line.
    private ParticipantSessionMembershipDecisionDto Deny(Guid liveSessionId, Guid teamId, string reasonCode)
    {
        _logger.LogWarning(
            "Session membership check for session {LiveSessionId} team {TeamId} could not be completed ({ReasonCode}); denying access.",
            liveSessionId,
            teamId,
            reasonCode);

        return new ParticipantSessionMembershipDecisionDto(false, liveSessionId, teamId, reasonCode);
    }
}
