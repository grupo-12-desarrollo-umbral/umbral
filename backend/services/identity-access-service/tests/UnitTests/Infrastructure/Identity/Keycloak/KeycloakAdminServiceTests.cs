using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Infrastructure.Identity.Keycloak;

namespace umbral_backend.Application.UnitTests.Infrastructure.Identity.Keycloak;

// Exercises the SyncUserRoleAsync path against a mocked Keycloak Admin API (stub HttpMessageHandler):
// happy path, transient-retry-then-success, exhausted retries, and a deterministic non-retryable failure.
public sealed class KeycloakAdminServiceTests
{
    private const string TokenPath = "/protocol/openid-connect/token";
    private const string RolesPath = "/roles";
    private const string RoleMappingsPath = "/role-mappings/realm";

    [Fact]
    public async Task SyncUserRoleAsync_HappyPath_AssignsTargetRole()
    {
        var handler = new StubHandler(req => Ok(req));
        var service = CreateService(handler);

        await service.SyncUserRoleAsync("kc-user", Role.Operator, CancellationToken.None);

        handler.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath.EndsWith(RoleMappingsPath));
    }

    [Fact]
    public async Task SyncUserRoleAsync_TransientFailureThenSuccess_Retries()
    {
        var tokenCalls = 0;
        var handler = new StubHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith(TokenPath) && ++tokenCalls == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            return Ok(req);
        });
        var service = CreateService(handler);

        await service.SyncUserRoleAsync("kc-user", Role.Operator, CancellationToken.None);

        tokenCalls.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task SyncUserRoleAsync_KeycloakAlwaysUnavailable_ThrowsAfterRetries()
    {
        var tokenCalls = 0;
        var handler = new StubHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith(TokenPath))
            {
                tokenCalls++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            return Ok(req);
        });
        var service = CreateService(handler, maxAttempts: 3);

        var act = async () => await service.SyncUserRoleAsync("kc-user", Role.Operator, CancellationToken.None);

        await act.Should().ThrowAsync<IdentityProviderRoleSyncException>();
        tokenCalls.Should().Be(3);
    }

    [Fact]
    public async Task SyncUserRoleAsync_RealmRoleMissing_ThrowsWithoutRetrying()
    {
        var roleCalls = 0;
        var handler = new StubHandler(req =>
        {
            if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.EndsWith(RolesPath))
            {
                roleCalls++;
                return Json("[]"); // realm has no roles → target not found
            }

            return Ok(req);
        });
        var service = CreateService(handler, maxAttempts: 3);

        var act = async () => await service.SyncUserRoleAsync("kc-user", Role.Operator, CancellationToken.None);

        await act.Should().ThrowAsync<IdentityProviderRoleSyncException>();
        roleCalls.Should().Be(1); // deterministic failure is not retried
    }

    [Fact]
    public async Task SyncUserActiveStateAsync_Disable_PutsEnabledFalseOnUser()
    {
        var handler = new StubHandler(req => Ok(req));
        var service = CreateService(handler);

        await service.SyncUserActiveStateAsync("kc-user", isActive: false, CancellationToken.None);

        var put = handler.Requests.SingleOrDefault(r =>
            r.Method == HttpMethod.Put && r.RequestUri!.AbsolutePath.EndsWith("/users/kc-user"));
        put.Should().NotBeNull();
        (await put!.Content!.ReadAsStringAsync()).Should().Contain("\"enabled\":false");
    }

    [Fact]
    public async Task SyncUserActiveStateAsync_AlreadyDisabled_IsIdempotent()
    {
        // Keycloak 204s on an unconditional PUT enabled=false regardless of prior state, so a
        // repeat deactivation is a no-op that still completes successfully.
        var handler = new StubHandler(req => Ok(req));
        var service = CreateService(handler);

        var act = async () => await service.SyncUserActiveStateAsync("kc-user", isActive: false, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SyncUserActiveStateAsync_TransientFailureThenSuccess_Retries()
    {
        var tokenCalls = 0;
        var handler = new StubHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith(TokenPath) && ++tokenCalls == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            return Ok(req);
        });
        var service = CreateService(handler);

        await service.SyncUserActiveStateAsync("kc-user", isActive: false, CancellationToken.None);

        tokenCalls.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task SyncUserActiveStateAsync_KeycloakAlwaysUnavailable_ThrowsAfterRetries()
    {
        var putCalls = 0;
        var handler = new StubHandler(req =>
        {
            if (req.Method == HttpMethod.Put)
            {
                putCalls++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            return Ok(req);
        });
        var service = CreateService(handler, maxAttempts: 3);

        var act = async () => await service.SyncUserActiveStateAsync("kc-user", isActive: false, CancellationToken.None);

        await act.Should().ThrowAsync<IdentityProviderUserStateSyncException>();
        putCalls.Should().Be(3);
    }

    [Fact]
    public async Task GetAdminToken_UsesClientCredentialsGrantWithClientCredentials()
    {
        // The security contract of #140: the admin token is minted via client_credentials with the
        // confidential client's id/secret — never a master-realm password grant.
        var handler = new StubHandler(req => Ok(req));
        var service = CreateService(handler);

        await service.SyncUserActiveStateAsync("kc-user", isActive: false, CancellationToken.None);

        var tokenRequest = handler.Requests.SingleOrDefault(r =>
            r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath.EndsWith(TokenPath));
        tokenRequest.Should().NotBeNull();

        var body = await tokenRequest!.Content!.ReadAsStringAsync();
        body.Should().Contain("grant_type=client_credentials");
        body.Should().Contain("client_id=umbral-backend");
        body.Should().Contain("client_secret=umbral-backend-dev-secret");
    }

    private static KeycloakAdminService CreateService(StubHandler handler, int maxAttempts = 3)
    {
        var options = Options.Create(new KeycloakOptions
        {
            AdminAuthority = "http://keycloak",
            Realm = "umbral",
            SyncMaxAttempts = maxAttempts,
            SyncRetryBaseDelayMs = 0,
        });

        return new KeycloakAdminService(new HttpClient(handler), options, Mock.Of<ILogger<KeycloakAdminService>>());
    }

    // Default success response per endpoint the sync touches.
    private static HttpResponseMessage Ok(HttpRequestMessage req)
    {
        var path = req.RequestUri!.AbsolutePath;

        if (path.EndsWith(TokenPath))
        {
            return Json("{\"access_token\":\"token\"}");
        }

        if (req.Method == HttpMethod.Get && path.EndsWith(RolesPath))
        {
            return Json("[{\"id\":\"1\",\"name\":\"Operator\"},{\"id\":\"2\",\"name\":\"Participant\"}]");
        }

        if (req.Method == HttpMethod.Get && path.EndsWith(RoleMappingsPath))
        {
            return Json("[]"); // user currently has no realm roles
        }

        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_respond(request));
        }
    }
}
