using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Common;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Scores.EventHandlers;

public sealed class OutboxDomainEventDispatcher : IOutboxDomainEventDispatcher
{
    private readonly PublishScoreEntryRegisteredIntegrationEventHandler _scoreEntryRegistered;

    public OutboxDomainEventDispatcher(
        PublishScoreEntryRegisteredIntegrationEventHandler scoreEntryRegistered)
    {
        _scoreEntryRegistered = scoreEntryRegistered;
    }

    public Task DispatchAsync(BaseEvent domainEvent, CancellationToken cancellationToken) => domainEvent switch
    {
        ScoreEntryRegistered scoreEntryRegistered =>
            _scoreEntryRegistered.Handle(scoreEntryRegistered, cancellationToken),
        _ => Task.CompletedTask,
    };
}
