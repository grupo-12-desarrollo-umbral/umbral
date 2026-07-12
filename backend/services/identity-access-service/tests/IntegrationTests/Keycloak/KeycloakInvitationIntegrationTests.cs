using System.Net.Http.Headers;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using umbral_backend.Domain.Enums;
using umbral_backend.Infrastructure.Identity.Keycloak;

namespace umbral_backend.Infrastructure.IntegrationTests.Keycloak;

// End-to-end proof of the admin-initiated invitation against a real Keycloak (+ Mailpit SMTP sink):
// KeycloakAdminService creates the enabled, email-unverified, credential-less account, assigns the
// requested realm role, and dispatches the required-actions email. Asserts the account state and role
// on Keycloak, and that the invitation email actually left Keycloak (landed in Mailpit).
[Trait("Category", "Keycloak")]
public sealed class KeycloakInvitationIntegrationTests : IAsyncLifetime
{
    private const string Realm = "umbral";
    private const string ClientId = "umbral-backend";
    private const string ClientSecret = "umbral-backend-dev-secret";
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
