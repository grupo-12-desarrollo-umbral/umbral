namespace ApiGateway.Transforms;

public static class WebSocketTokenExtractionTransform
{
    /// <summary>
    /// The query key SignalR uses to deliver its bearer token (ADR-0002). Read here during gateway
    /// authentication and removed from the forwarded query by <see cref="TrustedHeadersTransform"/>.
    /// </summary>
    public const string AccessTokenQueryKey = "access_token";

    public static Task OnMessageReceived(MessageReceivedContext context)
    {
        // Browser WS clients cannot set Authorization headers; SignalR passes token as ?access_token.
        if (context.Request.Query.TryGetValue(AccessTokenQueryKey, out var token) &&
            context.Request.Headers.TryGetValue("Upgrade", out var upgrade) &&
            upgrade.ToString().Equals("websocket", StringComparison.OrdinalIgnoreCase))
        {
            context.Token = token;
        }

        return Task.CompletedTask;
    }
}
