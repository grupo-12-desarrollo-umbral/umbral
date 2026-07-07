using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Infrastructure.Identity.Keycloak;

public sealed class KeycloakAdminService : IIdentityProviderAdminService
{
    private readonly HttpClient _httpClient;
    private readonly KeycloakOptions _options;
    private readonly ILogger<KeycloakAdminService> _logger;

    public KeycloakAdminService(
        HttpClient httpClient,
        IOptions<KeycloakOptions> options,
        ILogger<KeycloakAdminService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    // Bounded retry over the whole sync operation. Transient failures (connection/5xx/timeout)
    // are retried; on exhaustion — or a deterministic failure like a missing realm role — the
    // caller gets an IdentityProviderRoleSyncException (503). Failures are never swallowed, so
    // the handler (Keycloak-first) never commits the DB when Keycloak is out of reach.
    // ponytail: in-process retry; swap for an outbox/queue only if durable at-least-once is needed.
    public async Task SyncUserRoleAsync(string externalIdentityId, Role newRole, CancellationToken cancellationToken)
    {
        var realmRole = newRole.ToString();
        var maxAttempts = Math.Max(1, _options.SyncMaxAttempts);
        Exception? lastTransientError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await SyncOnceAsync(externalIdentityId, realmRole, cancellationToken);
                _logger.LogInformation("Keycloak role for user {UserId} synced to {Role}", externalIdentityId, realmRole);
                return;
            }
            catch (Exception ex) when (IsTransient(ex) && !cancellationToken.IsCancellationRequested)
            {
                lastTransientError = ex;
                _logger.LogWarning(
                    ex, "Keycloak role sync attempt {Attempt}/{Max} failed for user {UserId}; retrying",
                    attempt, maxAttempts, externalIdentityId);

                if (attempt < maxAttempts)
                {
                    await Task.Delay(_options.SyncRetryBaseDelayMs * attempt, cancellationToken);
                }
            }
        }

        throw new IdentityProviderRoleSyncException(
            externalIdentityId, realmRole, "Keycloak Admin API unavailable after retries.", lastTransientError!);
    }

    private async Task SyncOnceAsync(string externalIdentityId, string realmRole, CancellationToken cancellationToken)
    {
        var token = await GetAdminTokenAsync(cancellationToken);

        var availableRoles = await GetRealmRolesAsync(token, cancellationToken);
        var targetRole = availableRoles.FirstOrDefault(r =>
            string.Equals(r.Name, realmRole, StringComparison.OrdinalIgnoreCase));

        if (targetRole is null)
        {
            // Deterministic: retrying will not conjure the role. Surface it — do not swallow.
            throw new IdentityProviderRoleSyncException(
                externalIdentityId, realmRole, $"Realm role '{realmRole}' not found in Keycloak.");
        }

        var currentRoles = await GetUserRealmRolesAsync(token, externalIdentityId, cancellationToken);
        var rolesToRemove = currentRoles
            .Where(r => !string.Equals(r.Name, realmRole, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (rolesToRemove.Count > 0)
        {
            await RemoveRealmRolesAsync(token, externalIdentityId, rolesToRemove, cancellationToken);
        }

        var alreadyAssigned = currentRoles.Any(r =>
            string.Equals(r.Name, realmRole, StringComparison.OrdinalIgnoreCase));

        if (!alreadyAssigned)
        {
            await AddRealmRoleAsync(token, externalIdentityId, targetRole, cancellationToken);
        }
    }

    // Transient = worth retrying. A non-transient IdentityProviderRoleSyncException (e.g. missing
    // realm role) propagates immediately; caller cancellation is never retried.
    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException || ex is TaskCanceledException || ex is TimeoutException;

    private async Task<string> GetAdminTokenAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsync(
            $"{_options.AdminAuthority}/realms/master/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = "admin-cli",
                ["username"] = _options.AdminUsername,
                ["password"] = _options.AdminPassword,
                ["grant_type"] = "password",
            }),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        return result!.AccessToken;
    }

    private async Task<List<RoleRepresentation>> GetRealmRolesAsync(string token, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_options.AdminAuthority}/admin/realms/{_options.Realm}/roles");

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<RoleRepresentation>>(cancellationToken) ?? [];
    }

    private async Task<List<RoleRepresentation>> GetUserRealmRolesAsync(
        string token, string userId, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_options.AdminAuthority}/admin/realms/{_options.Realm}/users/{userId}/role-mappings/realm");

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<RoleRepresentation>>(cancellationToken) ?? [];
    }

    private async Task AddRealmRoleAsync(
        string token, string userId, RoleRepresentation role, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.AdminAuthority}/admin/realms/{_options.Realm}/users/{userId}/role-mappings/realm")
        {
            Content = JsonContent.Create(new[] { role }),
        };

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task RemoveRealmRolesAsync(
        string token, string userId, List<RoleRepresentation> roles, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{_options.AdminAuthority}/admin/realms/{_options.Realm}/users/{userId}/role-mappings/realm")
        {
            Content = JsonContent.Create(roles),
        };

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;
    }

    private sealed record RoleRepresentation
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;
    }
}
