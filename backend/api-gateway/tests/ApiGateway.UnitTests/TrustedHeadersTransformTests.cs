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

    private static RequestTransformContext BuildAuthenticatedContext(string realmAccessJson)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = QueryString.Empty;
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "external-id"),
                new Claim("email", "user@umbral.local"),
                new Claim("realm_access", realmAccessJson),
            },
            authenticationType: "Test"));

        return new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = new HttpRequestMessage(),
            Query = new QueryTransformContext(httpContext.Request),
        };
    }
}
