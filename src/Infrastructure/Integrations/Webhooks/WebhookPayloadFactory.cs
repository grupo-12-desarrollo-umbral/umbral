namespace umbral_backend.Infrastructure.Integrations.Webhooks;

public static class WebhookPayloadFactory
{
    public static object Create(string eventType, object data) => new
    {
        Event = eventType,
        Data = data,
        GeneratedAt = DateTimeOffset.UtcNow
    };
}
