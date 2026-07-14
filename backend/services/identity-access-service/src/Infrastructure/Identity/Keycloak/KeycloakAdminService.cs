using System.Net;
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

    // Create an invited user: enabled, email unverified, no credentials. Created enabled because
    // Keycloak refuses to send a required-actions email to a disabled user; the invitation handler
    // compensates with DeleteUserAsync if a later step fails, so a failed invitation leaves no orphan.
    // A single attempt — a POST is not idempotent, so a blind retry after a created-but-lost-response
    // would 409. Returns the new user's Keycloak id parsed from the Location header.
    public async Task<string> CreateUserAsync(string email, CancellationToken cancellationToken)
    {
        var token = await GetAdminTokenAsync(cancellationToken);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.AdminAuthority}/admin/realms/{_options.Realm}/users")
        {
            Content = JsonContent.Create(new
            {
                username = email,
                email,
                enabled = true,
                emailVerified = false,
            }),
        };

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new InvitedEmailAlreadyRegisteredException(email);
        }

        await EnsureSuccessOrThrowAsync(response, "create user", cancellationToken);

        var location = response.Headers.Location
            ?? throw new InvalidOperationException("Keycloak did not return a Location header for the created user.");

        _logger.LogInformation("Keycloak user created for invited email {Email}", email);

        return location.Segments[^1].Trim('/');
    }

    // Create a self-registering participant: enabled, email unverified, and carrying the password the
    // person chose on the mobile form (temporary: false — it is their real password, not a reset seed).
    // Created enabled so Keycloak will send the verification email; the register handler compensates
    // with DeleteUserAsync if a later step fails. Single attempt — a POST is not idempotent. Returns the
    // new user's Keycloak id from the Location header. A 409 surfaces as EmailAlreadyRegisteredException.
    public async Task<string> CreateParticipantAsync(
        string displayName, string email, string password, CancellationToken cancellationToken)
    {
        var token = await GetAdminTokenAsync(cancellationToken);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.AdminAuthority}/admin/realms/{_options.Realm}/users")
        {
            Content = JsonContent.Create(new
            {
                username = email,
                email,
                firstName = displayName,
                enabled = true,
                emailVerified = false,
                credentials = new[]
                {
                    new { type = "password", value = password, temporary = false },
                },
            }),
        };

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new EmailAlreadyRegisteredException(email);
        }

        await EnsureSuccessOrThrowAsync(response, "create participant", cancellationToken);

        var location = response.Headers.Location
            ?? throw new InvalidOperationException("Keycloak did not return a Location header for the created user.");

        _logger.LogInformation("Keycloak participant created for email {Email}", email);

        return location.Segments[^1].Trim('/');
    }

    // Ask Keycloak to email the invitee a required-actions link for UPDATE_PASSWORD, UPDATE_PROFILE and VERIFY_EMAIL.
    // Relies on the realm's configured SMTP server; a delivery/config failure surfaces (with Keycloak's
    // reason) so the invitation handler can compensate instead of leaving an orphaned account.
    public Task SendExecuteActionsEmailAsync(string externalIdentityId, CancellationToken cancellationToken) =>
        ExecuteActionsEmailAsync(
            externalIdentityId, new[] { "UPDATE_PASSWORD", "UPDATE_PROFILE", "VERIFY_EMAIL" }, cancellationToken);

    // Self-registered participants already set their password on the form, so only VERIFY_EMAIL is
    // required — email verification stays delegated to Keycloak (ADR-0016 §1).
    public Task SendVerifyEmailAsync(string externalIdentityId, CancellationToken cancellationToken) =>
        ExecuteActionsEmailAsync(externalIdentityId, new[] { "VERIFY_EMAIL" }, cancellationToken);

    // Anonymous forgot-password (ADR-0016 §1): email an UPDATE_PASSWORD-only action link. Keycloak owns
    // the reset flow; we only trigger the mail, mirroring SendVerifyEmailAsync.
    public Task SendResetPasswordEmailAsync(string externalIdentityId, CancellationToken cancellationToken) =>
        ExecuteActionsEmailAsync(externalIdentityId, new[] { "UPDATE_PASSWORD" }, cancellationToken);

    // Resolve the Keycloak user id for an exact email match, or null if none. exact=true keeps Keycloak
    // from returning substring matches. Backs the anonymous forgot-password flow: a null result lets the
    // handler stay silent so the endpoint never discloses whether an address is registered.
    public async Task<string?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var token = await GetAdminTokenAsync(cancellationToken);

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_options.AdminAuthority}/admin/realms/{_options.Realm}/users?email={Uri.EscapeDataString(email)}&exact=true");

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, "find user by email", cancellationToken);

        var users = await response.Content.ReadFromJsonAsync<List<UserRepresentation>>(cancellationToken) ?? [];
        return users.FirstOrDefault()?.Id;
    }

    private async Task ExecuteActionsEmailAsync(
        string externalIdentityId, string[] actions, CancellationToken cancellationToken)
    {
        var token = await GetAdminTokenAsync(cancellationToken);

        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"{_options.AdminAuthority}/admin/realms/{_options.Realm}/users/{externalIdentityId}/execute-actions-email")
        {
            Content = JsonContent.Create(actions),
        };

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, "send execute-actions email", cancellationToken);

        _logger.LogInformation("Keycloak execute-actions email dispatched to user {UserId}", externalIdentityId);
    }

    // Compensating delete for a failed invitation. Idempotent: a 404 is treated as already-removed so
    // the compensation never masks the original failure with a spurious one.
    public async Task DeleteUserAsync(string externalIdentityId, CancellationToken cancellationToken)
    {
        var token = await GetAdminTokenAsync(cancellationToken);

        var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{_options.AdminAuthority}/admin/realms/{_options.Realm}/users/{externalIdentityId}");

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        await EnsureSuccessOrThrowAsync(response, "delete user", cancellationToken);

        _logger.LogInformation("Keycloak user {UserId} deleted (invitation compensation)", externalIdentityId);
    }

    // EnsureSuccessStatusCode discards the response body; Keycloak returns an actionable reason there
    // (e.g. "User is disabled"), so surface it in the exception message for diagnosis.
    private static async Task EnsureSuccessOrThrowAsync(
        HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Keycloak {operation} failed with {(int)response.StatusCode} {response.StatusCode}: {body}");
    }

    // Keycloak-first role propagation, retried through the shared bounded-retry loop. On
    // exhaustion the caller gets an IdentityProviderRoleSyncException (503); a deterministic
    // failure like a missing realm role surfaces immediately without retrying.
    public Task SyncUserRoleAsync(string externalIdentityId, Role newRole, CancellationToken cancellationToken)
    {
        var realmRole = newRole.ToString();

        return RunWithBoundedRetryAsync(
            "role",
            externalIdentityId,
            ct => SyncOnceAsync(externalIdentityId, realmRole, ct),
            () => _logger.LogInformation("Keycloak role for user {UserId} synced to {Role}", externalIdentityId, realmRole),
            inner => new IdentityProviderRoleSyncException(
                externalIdentityId, realmRole, "Keycloak Admin API unavailable after retries.", inner!),
            cancellationToken);
    }

    // Keycloak-first enable/disable of the account. An unconditional PUT enabled=<isActive> is
    // idempotent — Keycloak 204s whether or not the account already had that state. Same
    // bounded-retry/fail-loud contract as SyncUserRoleAsync, so the handler never commits the
    // app DB when Keycloak is unreachable.
    public Task SyncUserActiveStateAsync(string externalIdentityId, bool isActive, CancellationToken cancellationToken)
    {
        return RunWithBoundedRetryAsync(
            "active-state",
            externalIdentityId,
            ct => SetUserEnabledAsync(externalIdentityId, isActive, ct),
            () => _logger.LogInformation("Keycloak active-state for user {UserId} synced to {IsActive}", externalIdentityId, isActive),
            inner => new IdentityProviderUserStateSyncException(
                externalIdentityId, isActive, "Keycloak Admin API unavailable after retries.", inner!),
            cancellationToken);
    }

    // Bounded retry over a whole sync operation. Transient failures (connection/5xx/timeout) are
    // retried; on exhaustion the caller-supplied onExhausted exception is thrown. A non-transient
    // domain exception raised by the operation (e.g. missing realm role) escapes the catch filter
    // and propagates immediately. Failures are never swallowed, so a Keycloak-first handler never
    // commits the DB when Keycloak is out of reach.
    // ponytail: in-process retry; swap for an outbox/queue only if durable at-least-once is needed.
    private async Task RunWithBoundedRetryAsync(
        string operation,
        string externalIdentityId,
        Func<CancellationToken, Task> attempt,
        Action onSuccess,
        Func<Exception?, Exception> onExhausted,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(1, _options.SyncMaxAttempts);
        Exception? lastTransientError = null;

        for (var attemptNumber = 1; attemptNumber <= maxAttempts; attemptNumber++)
        {
            try
            {
                await attempt(cancellationToken);
                onSuccess();
                return;
            }
            catch (Exception ex) when (IsTransient(ex) && !cancellationToken.IsCancellationRequested)
            {
                lastTransientError = ex;
                _logger.LogWarning(
                    ex, "Keycloak {Operation} sync attempt {Attempt}/{Max} failed for user {UserId}; retrying",
                    operation, attemptNumber, maxAttempts, externalIdentityId);

                if (attemptNumber < maxAttempts)
                {
                    await Task.Delay(_options.SyncRetryBaseDelayMs * attemptNumber, cancellationToken);
                }
            }
        }

        throw onExhausted(lastTransientError);
    }

    private async Task SetUserEnabledAsync(string externalIdentityId, bool isActive, CancellationToken cancellationToken)
    {
        var token = await GetAdminTokenAsync(cancellationToken);

        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"{_options.AdminAuthority}/admin/realms/{_options.Realm}/users/{externalIdentityId}")
        {
            Content = JsonContent.Create(new { enabled = isActive }),
        };

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
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
        // Client-credentials against the umbral realm: the umbral-backend service account carries
        // only manage-users + view-realm, so nothing here holds master-realm authority.
        var response = await _httpClient.PostAsync(
            $"{_options.AdminAuthority}/realms/{_options.Realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["grant_type"] = "client_credentials",
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

    private sealed record UserRepresentation
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;
    }
}
