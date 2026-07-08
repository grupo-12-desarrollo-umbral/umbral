using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Infrastructure.Identity;

/// <summary>
/// HTTP adapter that resolves the participant eligible-teams whitelist (#107) from the
/// identity-access-service. External integration glue — excluded from coverage.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ParticipantEligibleTeamsClient : IParticipantEligibleTeamsClient
{
    private const string RequestUri = "/api/permissions/participant-eligible-teams";
    private const string UnavailableReasonCode = "users-unavailable";

    private readonly HttpClient _httpClient;
    private readonly ICurrentUser _currentUser;

    public ParticipantEligibleTeamsClient(HttpClient httpClient, ICurrentUser currentUser)
    {
        _httpClient = httpClient;
        _currentUser = currentUser;
    }

    public async Task<ParticipantEligibleTeamsDto> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, RequestUri);
            ForwardTrustedHeaders(requestMessage);

            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Unavailable($"Users returned HTTP {(int)response.StatusCode}.");
            }

            var whitelist = await response.Content.ReadFromJsonAsync<ParticipantEligibleTeamsDto>(cancellationToken);
            return whitelist ?? Unavailable("Users returned an empty eligible-teams response.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable("Users eligible-teams check timed out.");
        }
        catch (HttpRequestException)
        {
            return Unavailable("Users eligible-teams check is unavailable.");
        }
        catch (JsonException)
        {
            return Unavailable("Users returned an unreadable eligible-teams response.");
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

    // Fail closed: an unreachable Users service yields no whitelist, so no team is joinable.
    private static ParticipantEligibleTeamsDto Unavailable(string reason)
    {
        _ = reason;
        return new ParticipantEligibleTeamsDto(false, UnavailableReasonCode, Array.Empty<EligibleTeamDto>());
    }
}
