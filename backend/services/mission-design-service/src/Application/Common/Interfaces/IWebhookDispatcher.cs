namespace umbral_backend.Application.Common.Interfaces;

public interface IWebhookDispatcher
{
    Task DispatchAsync(string eventType, object payload, CancellationToken cancellationToken = default);
}
