namespace umbral_backend.Infrastructure.Integrations.Webhooks;

public class WebhookOptions
{
    public const string SectionName = "Webhooks";

    public string TargetUrl { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public int RetryCount { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;
}
