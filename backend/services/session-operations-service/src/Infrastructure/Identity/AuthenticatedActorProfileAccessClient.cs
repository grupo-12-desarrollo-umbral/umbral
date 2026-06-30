using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Infrastructure.Identity;

[ExcludeFromCodeCoverage]
public sealed class AuthenticatedActorProfileAccessClient : IAuthenticatedActorProfileAccessClient
{
    private readonly HttpClient _httpClient;
    private readonly ICurrentUser _currentUser;

    public AuthenticatedActorProfileAccessClient(HttpClient httpClient, ICurrentUser currentUser)
    {
        _httpClient = httpClient;
        _currentUser = currentUser;
    }

    public async Task<AuthenticatedActorProfileLookupDto> GetCurrentAsync(CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        ForwardTrustedHeaders(requestMessage);

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AuthenticatedActorProfileResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Identity-access-service returned an empty authenticated actor profile.");

        return new AuthenticatedActorProfileLookupDto(
            payload.UserId,
            payload.ExternalIdentityId,
            payload.Role,
            payload.IsActive);
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

    private sealed record AuthenticatedActorProfileResponse(
        int UserId,
        string ExternalIdentityId,
        string DisplayName,
        string Email,
        string Role,
        bool IsActive);
}
