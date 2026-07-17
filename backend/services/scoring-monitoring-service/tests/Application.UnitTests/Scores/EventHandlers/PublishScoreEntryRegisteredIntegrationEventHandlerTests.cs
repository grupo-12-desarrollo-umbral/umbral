using MassTransit;
using umbral_backend.Application.Scores.Common;
using umbral_backend.Application.Scores.EventHandlers;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.Scores.EventHandlers;

public sealed class PublishScoreEntryRegisteredIntegrationEventHandlerTests
{
    [Fact]
    public async Task Handle_PublishesExactlyOneIntegrationEvent()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var notification = Event();
        var handler = new PublishScoreEntryRegisteredIntegrationEventHandler(publishEndpoint.Object);

        await handler.Handle(notification, CancellationToken.None);

        publishEndpoint.Verify(
            endpoint => endpoint.Publish(
                It.Is<ScoreEntryRegisteredIntegrationEvent>(integrationEvent =>
                    integrationEvent.ScoreEntryId == notification.ScoreEntryId &&
                    integrationEvent.LiveSessionId == notification.LiveSessionId &&
                    integrationEvent.TeamId == notification.TeamId &&
                    integrationEvent.SourceEntityId == notification.SourceEntityId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPublishThrows_Propagates()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint
            .Setup(endpoint => endpoint.Publish(It.IsAny<ScoreEntryRegisteredIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unreachable"));

        var handler = new PublishScoreEntryRegisteredIntegrationEventHandler(publishEndpoint.Object);

        var act = () => handler.Handle(Event(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("broker unreachable");
    }

    [Fact]
    public async Task Handle_ForwardsTheCallerCancellationToken()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var notification = Event();
        var handler = new PublishScoreEntryRegisteredIntegrationEventHandler(publishEndpoint.Object);
        using var cts = new CancellationTokenSource();

        await handler.Handle(notification, cts.Token);

        publishEndpoint.Verify(
            endpoint => endpoint.Publish(
                It.IsAny<ScoreEntryRegisteredIntegrationEvent>(),
                cts.Token),
            Times.Once);
    }

    private static ScoreEntryRegistered Event()
    {
        return new ScoreEntryRegistered(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ScoreEntryType.Grant,
            "trivia-answer-correct",
            100,
            new DateTimeOffset(2026, 7, 14, 18, 30, 0, TimeSpan.Zero),
            ScoreSourceType.TriviaAnswerSubmission,
            Guid.NewGuid(),
            recordedByUserId: null);
    }
}
