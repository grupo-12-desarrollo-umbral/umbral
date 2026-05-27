using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Infrastructure.Integrations.Webhooks;

public class WebhookDispatcher : IWebhookDispatcher
{
    private readonly HttpClient _httpClient;
    private readonly WebhookOptions _options;

    public WebhookDispatcher(HttpClient httpClient, IOptions<WebhookOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task DispatchAsync(string eventType, object payload, CancellationToken cancellationToken = default)
    {
        var envelope = new { EventType = eventType, Payload = payload, Timestamp = DateTimeOffset.UtcNow };
        await _httpClient.PostAsJsonAsync(_options.TargetUrl, envelope, cancellationToken);
    }
}
