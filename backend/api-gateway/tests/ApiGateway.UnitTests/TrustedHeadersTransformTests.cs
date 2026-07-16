using System.Security.Claims;
using ApiGateway.Transforms;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;
using Yarp.ReverseProxy.Transforms;

namespace ApiGateway.UnitTests;

// Guards the credential-stripping seam: the gateway authenticates the caller, then must not forward
// the raw token downstream by either door (Authorization header, or SignalR's ?access_token query).
public sealed class TrustedHeadersTransformTests
{
    [Fact]
    public async Task RemovesAccessTokenFromTheForwardedQuery()
    {
        var context = BuildContext("?access_token=eyJhbGciOiJSUzI1NiJ9.payload.sig");

        await new TrustedHeadersTransform().ApplyAsync(context);

        context.Query.QueryString.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task LeavesNonSensitiveQueryParametersIntact()
    {
        var context = BuildContext("?page=2&access_token=eyJsecret&sort=name");

        await new TrustedHeadersTransform().ApplyAsync(context);

        context.Query.QueryString.Value.Should().NotContain("access_token");
        context.Query.QueryString.Value.Should().Contain("page=2");
        context.Query.QueryString.Value.Should().Contain("sort=name");
    }

    [Fact]
    public async Task RemovesTheAuthorizationHeaderFromTheForwardedRequest()
    {
        var context = BuildContext("?page=2");
        context.ProxyRequest.Headers.TryAddWithoutValidation("Authorization", "Bearer eyJsecret");

        await new TrustedHeadersTransform().ApplyAsync(context);

        context.ProxyRequest.Headers.Contains("Authorization").Should().BeFalse();
    }

    // An unauthenticated request never reaches a protected route, but stripping is unconditional so
    // a future anonymous route cannot become a token-leaking one.
    [Fact]
    public async Task StripsTheTokenEvenWhenTheCallerIsNotAuthenticated()
    {
        var context = BuildContext("?access_token=eyJsecret");

        await new TrustedHeadersTransform().ApplyAsync(context);

        context.HttpContext.User.Identity?.IsAuthenticated.Should().NotBe(true);
        context.Query.QueryString.Value.Should().BeEmpty();
    }

    // A self-registered participant's Keycloak token carries the baseline roles alongside
    // Participant, and realm_access.roles has no guaranteed order. Whichever order Keycloak emits,
    // the gateway must forward the application role — never "offline_access" or "default-roles-*".
    [Theory]
    [InlineData("[\"Participant\",\"offline_access\",\"default-roles-umbral\",\"uma_authorization\"]")]
    [InlineData("[\"offline_access\",\"default-roles-umbral\",\"uma_authorization\",\"Participant\"]")]
    [InlineData("[\"default-roles-umbral\",\"uma_authorization\",\"offline_access\",\"Participant\"]")]
    public async Task ResolvesTheApplicationRoleRegardlessOfRealmRoleOrder(string realmRolesJson)
    {
        var context = BuildAuthenticatedContext($"{{\"roles\":{realmRolesJson}}}");

        await new TrustedHeadersTransform().ApplyAsync(context);

        HeaderValue(context, "X-User-Role").Should().Be("Participant");
    }

    // A seed operator holds a single realm role; behavior is unchanged.
    [Fact]
    public async Task ForwardsASingleApplicationRole()
    {
        var context = BuildAuthenticatedContext("{\"roles\":[\"Operator\"]}");

        await new TrustedHeadersTransform().ApplyAsync(context);

        HeaderValue(context, "X-User-Role").Should().Be("Operator");
    }

    // No realistic assignment gives a user two application roles, but if one ever appeared the
    // resolution must be the most-privileged (never silently downgrade an operator/admin), and it
    // must be order-independent. This pins that contract.
    [Theory]
    [InlineData("[\"Participant\",\"Operator\"]", "Operator")]
    [InlineData("[\"Operator\",\"Participant\"]", "Operator")]
    [InlineData("[\"Operator\",\"Administrator\",\"Participant\"]", "Administrator")]
    public async Task ResolvesTheMostPrivilegedRoleWhenSeveralArePresent(string realmRolesJson, string expected)
    {
        var context = BuildAuthenticatedContext($"{{\"roles\":{realmRolesJson}}}");

        await new TrustedHeadersTransform().ApplyAsync(context);

        HeaderValue(context, "X-User-Role").Should().Be(expected);
    }

    // A token with only Keycloak baseline roles has no application role, so no role header is sent
    // (downstream then rejects the caller rather than being handed an unrecognized role).
    [Fact]
    public async Task OmitsTheRoleHeaderWhenNoApplicationRoleIsPresent()
    {
        var context = BuildAuthenticatedContext("{\"roles\":[\"offline_access\",\"uma_authorization\"]}");

        await new TrustedHeadersTransform().ApplyAsync(context);

        context.ProxyRequest.Headers.Contains("X-User-Role").Should().BeFalse();
    }

    // A client must never supply the trusted identity headers. On an anonymous request (e.g. the
    // register route) they are the only identity present, so an un-stripped forgery would be believed
    // downstream. Stripping is unconditional.
    [Theory]
    [InlineData("X-User-Id")]
    [InlineData("X-User-Role")]
    [InlineData("X-User-Email")]
    [InlineData("X-User-Name")]
    public async Task StripsForgedIdentityHeadersOnAnUnauthenticatedRequest(string header)
    {
        var context = BuildContext(string.Empty);
        context.ProxyRequest.Headers.TryAddWithoutValidation(header, "Administrator");

        await new TrustedHeadersTransform().ApplyAsync(context);

        context.ProxyRequest.Headers.Contains(header).Should().BeFalse();
    }

    // When the caller forges an identity header AND is authenticated, the forgery is dropped before
    // the gateway re-adds its own value — so exactly one value (the token's) is forwarded, never the
    // client's alongside it.
    [Fact]
    public async Task OverwritesAForgedRoleHeaderWithTheTokenDerivedRole()
    {
        var context = BuildAuthenticatedContext("{\"roles\":[\"Operator\"]}");
        context.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Role", "Administrator");

        await new TrustedHeadersTransform().ApplyAsync(context);

        context.ProxyRequest.Headers.GetValues("X-User-Role").Should().ContainSingle()
            .Which.Should().Be("Operator");
    }

    // Keycloak's `profile` scope supplies `name`; it is the display name downstream shows for a
    // participant, so the gateway forwards it rather than leaving services to re-derive one.
    [Fact]
    public async Task ForwardsTheNameClaimAsTheDisplayNameHeader()
    {
        var context = BuildAuthenticatedContext("{\"roles\":[\"Participant\"]}");

        await new TrustedHeadersTransform().ApplyAsync(context);

        HeaderValue(context, "X-User-Name").Should().Be("Participant Umbral");
    }

    // A user whose Keycloak profile has no first/last name gets no `name` claim at all. Falling back
    // to preferred_username keeps a usable header instead of forwarding none.
    [Fact]
    public async Task FallsBackToPreferredUsernameWhenTheNameClaimIsAbsent()
    {
        var context = BuildAuthenticatedContext("{\"roles\":[\"Participant\"]}", name: null);

        await new TrustedHeadersTransform().ApplyAsync(context);

        HeaderValue(context, "X-User-Name").Should().Be("participant");
    }

    [Fact]
    public async Task OmitsTheNameHeaderWhenNoNameSourceExists()
    {
        var context = BuildAuthenticatedContext("{\"roles\":[\"Participant\"]}", name: null, preferredUsername: null);

        await new TrustedHeadersTransform().ApplyAsync(context);

        context.ProxyRequest.Headers.Contains("X-User-Name").Should().BeFalse();
    }

    private static string? HeaderValue(RequestTransformContext context, string name)
    {
        return context.ProxyRequest.Headers.TryGetValues(name, out var values)
            ? values.FirstOrDefault()
            : null;
    }

    private static RequestTransformContext BuildContext(string queryString)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString(queryString);

        return new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = new HttpRequestMessage(),
            Query = new QueryTransformContext(httpContext.Request),
        };
    }

    private static RequestTransformContext BuildAuthenticatedContext(
        string realmAccessJson,
        string? name = "Participant Umbral",
        string? preferredUsername = "participant")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = QueryString.Empty;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "external-id"),
            new("email", "user@umbral.local"),
            new("realm_access", realmAccessJson),
        };
        if (name is not null)
        {
            claims.Add(new Claim("name", name));
        }
        if (preferredUsername is not null)
        {
            claims.Add(new Claim("preferred_username", preferredUsername));
        }

        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));

        return new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = new HttpRequestMessage(),
            Query = new QueryTransformContext(httpContext.Request),
        };
    }
}
