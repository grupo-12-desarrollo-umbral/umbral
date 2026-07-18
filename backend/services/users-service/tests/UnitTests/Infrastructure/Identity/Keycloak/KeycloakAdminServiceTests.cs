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
    public async Task CreateUserAsync_CreatesEnabledUnverifiedUser_AndReturnsIdFromLocationHeader()
    {
        var handler = new StubHandler(req =>
        {
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath.EndsWith("/users"))
            {
                var created = new HttpResponseMessage(HttpStatusCode.Created);
                created.Headers.Location = new Uri("http://keycloak/admin/realms/umbral/users/new-kc-id");
                return created;
            }

            return Ok(req);
        });
        var service = CreateService(handler);

        var id = await service.CreateUserAsync("invitee@example.com", CancellationToken.None);

        id.Should().Be("new-kc-id");

        var post = handler.Requests.Single(r =>
            r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath.EndsWith("/users"));
        var body = await post.Content!.ReadAsStringAsync();
        // Enabled: Keycloak refuses to email a disabled user; the handler compensates on failure.
        body.Should().Contain("\"enabled\":true");
        body.Should().Contain("\"emailVerified\":false");
        body.Should().Contain("invitee@example.com");
        // No credentials are ever sent to Keycloak.
        body.Should().NotContain("credential");
        body.Should().NotContain("password");
    }

    [Fact]
    public async Task CreateParticipantAsync_CreatesEnabledUnverifiedUserWithPassword_AndReturnsIdFromLocationHeader()
    {
        var handler = new StubHandler(req =>
        {
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath.EndsWith("/users"))
            {
                var created = new HttpResponseMessage(HttpStatusCode.Created);
                created.Headers.Location = new Uri("http://keycloak/admin/realms/umbral/users/new-participant-id");
                return created;
            }

            return Ok(req);
        });
        var service = CreateService(handler);

        var id = await service.CreateParticipantAsync(
            "New Participant", "participant@example.com", "sup3rsecret", CancellationToken.None);

        id.Should().Be("new-participant-id");

        var post = handler.Requests.Single(r =>
            r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath.EndsWith("/users"));
        var body = await post.Content!.ReadAsStringAsync();
        // Enabled so Keycloak sends the verification email; unverified so the participant must verify.
        body.Should().Contain("\"enabled\":true");
        body.Should().Contain("\"emailVerified\":false");
        body.Should().Contain("participant@example.com");
        // The whole display name lands on firstName, and no surname is invented for it: Keycloak builds
        // the `name` claim from firstName + lastName, and that claim is the participant's display name.
        body.Should().Contain("\"firstName\":\"New Participant\"");
        body.Should().NotContain("lastName");
        // The chosen password is forwarded as a non-temporary credential (their real password).
        body.Should().Contain("\"type\":\"password\"");
        body.Should().Contain("\"value\":\"sup3rsecret\"");
        body.Should().Contain("\"temporary\":false");
    }

    [Fact]
    public async Task CreateParticipantAsync_WhenKeycloakReturnsConflict_ThrowsEmailAlreadyRegistered()
    {
        var handler = new StubHandler(req =>
        {
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath.EndsWith("/users"))
            {
                return new HttpResponseMessage(HttpStatusCode.Conflict);
            }

            return Ok(req);
        });
        var service = CreateService(handler);

        var act = async () => await service.CreateParticipantAsync(
            "New Participant", "participant@example.com", "sup3rsecret", CancellationToken.None);

        await act.Should().ThrowAsync<EmailAlreadyRegisteredException>();
    }

    [Fact]
    public async Task SendVerifyEmailAsync_PutsVerifyEmailAction_WithoutUpdatePassword()
    {
        var handler = new StubHandler(req => Ok(req));
        var service = CreateService(handler);

        await service.SendVerifyEmailAsync("kc-user", CancellationToken.None);

        var put = handler.Requests.SingleOrDefault(r =>
            r.Method == HttpMethod.Put && r.RequestUri!.AbsolutePath.EndsWith("/users/kc-user/execute-actions-email"));
        put.Should().NotBeNull();

        var body = await put!.Content!.ReadAsStringAsync();
        body.Should().Contain("VERIFY_EMAIL");
        // Participants set their password on the form up front — no UPDATE_PASSWORD action.
        body.Should().NotContain("UPDATE_PASSWORD");
    }

    [Fact]
    public async Task SendResetPasswordEmailAsync_PutsUpdatePasswordAction_WithoutVerifyEmail()
    {
        var handler = new StubHandler(req => Ok(req));
        var service = CreateService(handler);

        await service.SendResetPasswordEmailAsync("kc-user", CancellationToken.None);

        var put = handler.Requests.SingleOrDefault(r =>
            r.Method == HttpMethod.Put && r.RequestUri!.AbsolutePath.EndsWith("/users/kc-user/execute-actions-email"));
        put.Should().NotBeNull();

        var body = await put!.Content!.ReadAsStringAsync();
        body.Should().Contain("UPDATE_PASSWORD");
        // Forgot-password only resets the credential — no VERIFY_EMAIL/UPDATE_PROFILE actions.
        body.Should().NotContain("VERIFY_EMAIL");
    }

    [Fact]
    public async Task FindUserIdByEmailAsync_WhenUserExists_ReturnsIdFromExactMatch()
    {
        var handler = new StubHandler(req =>
        {
            if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.EndsWith("/users"))
            {
                return Json("[{\"id\":\"kc-found-id\",\"email\":\"participant@example.com\"}]");
            }

            return Ok(req);
        });
        var service = CreateService(handler);

        var id = await service.FindUserIdByEmailAsync("participant@example.com", CancellationToken.None);

        id.Should().Be("kc-found-id");
        var get = handler.Requests.Single(r =>
            r.Method == HttpMethod.Get && r.RequestUri!.AbsolutePath.EndsWith("/users"));
        // Exact match keeps Keycloak from returning substring hits.
        get.RequestUri!.Query.Should().Contain("exact=true");
        get.RequestUri.Query.Should().Contain("email=participant%40example.com");
    }

    [Fact]
    public async Task FindUserIdByEmailAsync_WhenNoUserMatches_ReturnsNull()
    {
        var handler = new StubHandler(req =>
        {
            if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.EndsWith("/users"))
            {
                return Json("[]");
            }

            return Ok(req);
        });
        var service = CreateService(handler);

        var id = await service.FindUserIdByEmailAsync("nobody@example.com", CancellationToken.None);

        id.Should().BeNull();
    }

    [Fact]
    public async Task DeleteUserAsync_SendsDelete_AndTreatsMissingUserAsAlreadyRemoved()
    {
        var handler = new StubHandler(req =>
            req.Method == HttpMethod.Delete
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : Ok(req));
        var service = CreateService(handler);

        var act = async () => await service.DeleteUserAsync("kc-user", CancellationToken.None);

        await act.Should().NotThrowAsync();
        handler.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Delete && r.RequestUri!.AbsolutePath.EndsWith("/users/kc-user"));
    }

    [Fact]
    public async Task CreateUserAsync_WhenKeycloakReturnsConflict_ThrowsEmailAlreadyRegistered()
    {
        var handler = new StubHandler(req =>
        {
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath.EndsWith("/users"))
            {
                return new HttpResponseMessage(HttpStatusCode.Conflict);
            }

            return Ok(req);
        });
        var service = CreateService(handler);

        var act = async () => await service.CreateUserAsync("invitee@example.com", CancellationToken.None);

        await act.Should().ThrowAsync<InvitedEmailAlreadyRegisteredException>();
    }

    [Fact]
    public async Task CreateUserAsync_WhenKeycloakRejects_ThrowsWithResponseBody()
    {
        var handler = new StubHandler(req =>
            req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath.EndsWith("/users")
                ? new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"errorMessage\":\"User is disabled\"}"),
                }
                : Ok(req));
        var service = CreateService(handler);

        var act = async () => await service.CreateUserAsync("invitee@example.com", CancellationToken.None);

        // EnsureSuccessStatusCode discards the body; the enriched failure keeps Keycloak's reason.
        (await act.Should().ThrowAsync<HttpRequestException>())
            .Which.Message.Should().Contain("User is disabled");
    }

    [Fact]
    public async Task SendExecuteActionsEmailAsync_PutsUpdatePasswordAndVerifyEmailActions()
    {
        var handler = new StubHandler(req => Ok(req));
        var service = CreateService(handler);

        await service.SendExecuteActionsEmailAsync("kc-user", CancellationToken.None);

        var put = handler.Requests.SingleOrDefault(r =>
            r.Method == HttpMethod.Put && r.RequestUri!.AbsolutePath.EndsWith("/users/kc-user/execute-actions-email"));
        put.Should().NotBeNull();

        var body = await put!.Content!.ReadAsStringAsync();
        body.Should().Contain("UPDATE_PASSWORD");
        body.Should().Contain("VERIFY_EMAIL");
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
