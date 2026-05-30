namespace Microsoft.AspNetCore.Authentication.JwtBearer;

/// <summary>
/// Intercepts backchannel HTTP requests from the JWT bearer middleware and rewrites
/// <c>localhost:8080</c> to <c>keycloak:8080</c> so that Keycloak metadata/JWKS
/// URLs published with the external hostname can still be resolved inside Docker.
/// </summary>
public sealed class RewriteLocalhostBackchannelHandler : DelegatingHandler
{
    public RewriteLocalhostBackchannelHandler(HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is { Host: "localhost", Port: 8080 })
        {
            var builder = new UriBuilder(request.RequestUri)
            {
                Host = "keycloak",
                Port = 8080
            };
            request.RequestUri = builder.Uri;
        }

        return base.SendAsync(request, cancellationToken);
    }
}
