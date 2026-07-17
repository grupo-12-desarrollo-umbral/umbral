using MassTransit;
using umbral_backend.Application.Scores.Common;
using umbral_backend.Application.Scores.EventHandlers;
using umbral_backend.Domain.Common;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.Scores.EventHandlers;

public sealed class OutboxDomainEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_ForScoreEntryRegistered_EnqueuesIntegrationEvent()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var dispatcher = new OutboxDomainEventDispatcher(
            new PublishScoreEntryRegisteredIntegrationEventHandler(publishEndpoint.Object));
        var domainEvent = ScoreEntryEvent();

        await dispatcher.DispatchAsync(domainEvent, CancellationToken.None);

        publishEndpoint.Verify(
            endpoint => endpoint.Publish(
                It.Is<ScoreEntryRegisteredIntegrationEvent>(message =>
                    message.ScoreEntryId == domainEvent.ScoreEntryId &&
                    message.LiveSessionId == domainEvent.LiveSessionId &&
                    message.TeamId == domainEvent.TeamId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ForUnmappedEvent_CompletesWithoutPublishing()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var dispatcher = new OutboxDomainEventDispatcher(
            new PublishScoreEntryRegisteredIntegrationEventHandler(publishEndpoint.Object));

        await dispatcher.DispatchAsync(new UnmappedEvent(), CancellationToken.None);

        publishEndpoint.VerifyNoOtherCalls();
    }

    private static ScoreEntryRegistered ScoreEntryEvent() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        ScoreEntryType.Grant,
        "trivia-answer-correct",
        100,
        new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero),
        ScoreSourceType.TriviaAnswerSubmission,
        Guid.NewGuid(),
        recordedByUserId: null);

    private sealed class UnmappedEvent : BaseEvent;
}
