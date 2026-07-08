using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Application.UnitTests.TestData;

/// <summary>
/// In-memory <see cref="IIntegrationEventPublisher"/> for the HU-33B publish-handler tests:
/// records what was published, or throws to prove the bridge swallows broker failures (D-3).
/// </summary>
public sealed class FakeIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly bool _throwOnPublish;

    public FakeIntegrationEventPublisher(bool throwOnPublish = false)
    {
        _throwOnPublish = throwOnPublish;
    }

    public List<object> Published { get; } = new();

    public Task PublishAsync<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken cancellationToken)
        where TIntegrationEvent : notnull
    {
        if (_throwOnPublish)
        {
            throw new InvalidOperationException("broker unavailable");
        }

        Published.Add(integrationEvent);
        return Task.CompletedTask;
    }
}
