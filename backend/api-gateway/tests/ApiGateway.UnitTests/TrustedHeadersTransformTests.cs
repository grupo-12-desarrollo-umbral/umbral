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
}
