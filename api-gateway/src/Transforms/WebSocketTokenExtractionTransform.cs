namespace ApiGateway.Transforms;

public static class WebSocketTokenExtractionTransform
{
    public static Task OnMessageReceived(MessageReceivedContext context)
    {
        // Browser WS clients cannot set Authorization headers; SignalR passes token as ?access_token.
        if (context.Request.Query.TryGetValue("access_token", out var token) &&
            context.Request.Headers.TryGetValue("Upgrade", out var upgrade) &&
            upgrade.ToString().Equals("websocket", StringComparison.OrdinalIgnoreCase))
        {
            context.Token = token;
        }

        return Task.CompletedTask;
    }
}
