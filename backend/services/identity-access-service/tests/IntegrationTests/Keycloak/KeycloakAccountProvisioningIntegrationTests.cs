using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using umbral_backend.Domain.Enums;
using umbral_backend.Infrastructure.Identity.Keycloak;

namespace umbral_backend.Infrastructure.IntegrationTests.Keycloak;

// End-to-end proof of both account-provisioning flows against a real Keycloak booted from the actual
// realm import (+ Mailpit SMTP sink): admin-initiated invitation, and mobile self-registration. Running
// against the real realm is the point — both flows depend on realm-level config that no unit test sees.
[Trait("Category", "Keycloak")]
public sealed class KeycloakAccountProvisioningIntegrationTests : IAsyncLifetime
{
    private const string Realm = "umbral";
    private const string ClientId = "umbral-backend";
    private const string ClientSecret = "umbral-backend-dev-secret";
    private const string MobileClientId = "umbral-mobile";
    private const ushort KeycloakHttpPort = 8080;
    private const ushort MailpitHttpPort = 8025;

    private readonly INetwork _network = new NetworkBuilder().Build();
    private IContainer _mailpit = null!;
    private IContainer _keycloak = null!;
    private readonly HttpClient _http = new();

    public async Task InitializeAsync()
    {
        await _network.CreateAsync();

        // Alias "mailpit" matches the realm import's SMTP host default (${KC_SMTP_HOST:mailpit}),
        // so Keycloak reaches this catcher without extra configuration.
        _mailpit = new ContainerBuilder()
            .WithImage("axllent/mailpit:v1.20")
            .WithNetwork(_network)
            .WithNetworkAliases("mailpit")
            .WithEnvironment("MP_SMTP_AUTH_ACCEPT_ANY", "true")
            .WithEnvironment("MP_SMTP_AUTH_ALLOW_INSECURE", "true")
            .WithPortBinding(MailpitHttpPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request.ForPort(MailpitHttpPort).ForPath("/")))
            .Build();

        await _mailpit.StartAsync();

        _keycloak = new ContainerBuilder()
            .WithImage("quay.io/keycloak/keycloak:26.2")
            .WithNetwork(_network)
            .WithResourceMapping(FindRealmImport(), "/opt/keycloak/data/import/")
            .WithCommand("start-dev", "--import-realm")
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", "admin")
            .WithPortBinding(KeycloakHttpPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request
                    .ForPort(KeycloakHttpPort)
                    .ForPath($"/realms/{Realm}/.well-known/openid-configuration")))
            .Build();

        await _keycloak.StartAsync();
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        if (_keycloak is not null)
        {
            await _keycloak.DisposeAsync();
        }

        if (_mailpit is not null)
        {
            await _mailpit.DisposeAsync();
        }

        await _network.DeleteAsync();
    }

    [Fact]
    public async Task Invite_CreatesEnabledUnverifiedAccount_WithRole_AndIssuesEmail()
    {
        var email = $"invitee-{Guid.NewGuid():N}@umbral.local";
        var service = new KeycloakAdminService(new HttpClient(), Options.Create(BuildOptions()), NullLogger<KeycloakAdminService>.Instance);

        var userId = await service.CreateUserAsync(email, CancellationToken.None);
        userId.Should().NotBeNullOrWhiteSpace();

        await service.SyncUserRoleAsync(userId, Role.Operator, CancellationToken.None);
        await service.SendExecuteActionsEmailAsync(userId, CancellationToken.None);

        var token = await GetAdminTokenAsync();

        var user = await GetUserAsync(token, userId);
        user.GetProperty("enabled").GetBoolean().Should().BeTrue();
        user.GetProperty("emailVerified").GetBoolean().Should().BeFalse();
        user.GetProperty("email").GetString().Should().Be(email);

        var roles = await GetRealmRoleNamesAsync(token, userId);
        roles.Should().Contain("Operator");

        // The invitation email actually left Keycloak and reached the SMTP sink.
        (await WaitForMailpitMessageAsync(email)).Should().BeTrue();
    }

    // The self-registration story mobile depends on: register, stay locked out until the address is
    // verified, then sign in by password grant with the chosen display name intact on the `name` claim.
    // Guards the realm's user-profile config. Keycloak's stock profile makes lastName required, and we
    // only ever send firstName — which left every self-registered account rejected at the password grant
    // with "Account is not fully set up", unrecoverably, since a password grant cannot run the browser
    // flow that would collect the missing field. A single-word display name is the strict case: the
    // validator allows it, so there is no surname to split out of it.
    [Theory]
    [InlineData("Nora")]
    [InlineData("Nora Smith")]
    public async Task SelfRegistration_LogsInOnceVerified_WithDisplayNameOnNameClaim(string displayName)
    {
        const string password = "s3lf-r3g-p4ss!";
        var email = $"participant-{Guid.NewGuid():N}@umbral.local";
        var service = new KeycloakAdminService(new HttpClient(), Options.Create(BuildOptions()), NullLogger<KeycloakAdminService>.Instance);

        var userId = await service.CreateParticipantAsync(displayName, email, password, CancellationToken.None);
        userId.Should().NotBeNullOrWhiteSpace();

        var token = await GetAdminTokenAsync();
        var user = await GetUserAsync(token, userId);
        user.GetProperty("firstName").GetString().Should().Be(displayName);
        // No surname is invented to satisfy Keycloak; the realm has to accept the account without one.
        user.TryGetProperty("lastName", out _).Should().BeFalse();

        // Email verification is a real gate (realm verifyEmail=true), not the bug: it is recoverable in
        // the app, because registration mails a VERIFY_EMAIL link. Pin it so it stays deliberate.
        (await PasswordGrantAsync(email, password)).Succeeded.Should().BeFalse();

        await CompleteEmailVerificationAsync(token, userId);

        var grant = await PasswordGrantAsync(email, password);
        grant.Succeeded.Should().BeTrue(
            "a verified self-registered account must be able to sign in, but Keycloak said {0}", grant.Error);
        // Keycloak concatenates firstName + lastName into `name`, which is the participant's display
        // name downstream — so a fabricated surname would leak into the UI verbatim.
        grant.Claims!.Value.GetProperty("name").GetString().Should().Be(displayName);
    }

    private KeycloakOptions BuildOptions() => new()
    {
        AdminAuthority = KeycloakBaseUrl(),
        Realm = Realm,
        ClientId = ClientId,
        ClientSecret = ClientSecret,
        SyncMaxAttempts = 3,
        SyncRetryBaseDelayMs = 0,
    };

    private string KeycloakBaseUrl() =>
        $"http://{_keycloak.Hostname}:{_keycloak.GetMappedPublicPort(KeycloakHttpPort)}";

    private string MailpitBaseUrl() =>
        $"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(MailpitHttpPort)}";

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _http.PostAsync(
            $"{KeycloakBaseUrl()}/realms/{Realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["grant_type"] = "client_credentials",
            }));

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("access_token").GetString()!;
    }

    private async Task<JsonElement> GetUserAsync(string token, string userId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"{KeycloakBaseUrl()}/admin/realms/{Realm}/users/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    // Stands in for the user clicking the VERIFY_EMAIL link Keycloak mailed them. Clearing
    // requiredActions is part of the simulation, not a shortcut: a login attempt made before verifying
    // stamps VERIFY_EMAIL onto the account, and Keycloak's action-token handler both marks the address
    // verified and drops that action. Setting emailVerified alone would leave the account blocked in a
    // way the real link never does.
    private async Task CompleteEmailVerificationAsync(string token, string userId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put, $"{KeycloakBaseUrl()}/admin/realms/{Realm}/users/{userId}")
        {
            Content = JsonContent.Create(new { emailVerified = true, requiredActions = Array.Empty<string>() }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    // Signs in exactly as mobile does (public client, password grant). Carries Keycloak's rejection
    // reason rather than just a null, so a regression names the gate that blocked the login.
    private sealed record GrantResult(JsonElement? Claims, string? Error)
    {
        public bool Succeeded => Claims is not null;
    }

    private async Task<GrantResult> PasswordGrantAsync(string email, string password)
    {
        var response = await _http.PostAsync(
            $"{KeycloakBaseUrl()}/realms/{Realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = MobileClientId,
                ["grant_type"] = "password",
                ["username"] = email,
                ["password"] = password,
                ["scope"] = "openid",
            }));

        var payload = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            return new GrantResult(null, $"{(int)response.StatusCode}: {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        var accessToken = document.RootElement.GetProperty("access_token").GetString()!;
        return new GrantResult(DecodeJwtPayload(accessToken), null);
    }

    private static JsonElement DecodeJwtPayload(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var padded = payload.Replace('-', '+').Replace('_', '/').PadRight((payload.Length + 3) / 4 * 4, '=');
        using var document = JsonDocument.Parse(Convert.FromBase64String(padded));
        return document.RootElement.Clone();
    }

    private async Task<IReadOnlyList<string>> GetRealmRoleNamesAsync(string token, string userId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"{KeycloakBaseUrl()}/admin/realms/{Realm}/users/{userId}/role-mappings/realm");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.EnumerateArray()
            .Select(role => role.GetProperty("name").GetString()!)
            .ToArray();
    }

    private async Task<bool> WaitForMailpitMessageAsync(string email)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var response = await _http.GetAsync($"{MailpitBaseUrl()}/api/v1/messages");
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var addressed = document.RootElement.GetProperty("messages").EnumerateArray()
                    .SelectMany(message => message.GetProperty("To").EnumerateArray())
                    .Any(recipient => string.Equals(
                        recipient.GetProperty("Address").GetString(), email, StringComparison.OrdinalIgnoreCase));

                if (addressed)
                {
                    return true;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        return false;
    }

    private static FileInfo FindRealmImport()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "deploy", "keycloak", "import", "umbral-realm.json");
            if (File.Exists(candidate))
            {
                return new FileInfo(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate deploy/keycloak/import/umbral-realm.json for the Keycloak test container.");
    }
}
