using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using umbral_backend.Application.Scores.Common;
using umbral_backend.Application.Scores.EventHandlers;
using umbral_backend.Domain.Events;

namespace umbral_backend.ScoringMonitoring.IntegrationTests.Messaging;

public sealed class PublishPenaltyAppliedIntegrationEventHandlerTests
{
    [Fact]
    public async Task Handle_PublishesExactlyOneIntegrationEvent()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var notification = PenaltyAppliedEvent();
        var handler = new PublishPenaltyAppliedIntegrationEventHandler(
            publishEndpoint.Object,
            NullLogger<PublishPenaltyAppliedIntegrationEventHandler>.Instance);

        await handler.Handle(notification, CancellationToken.None);

        publishEndpoint.Verify(
            endpoint => endpoint.Publish(
                It.Is<PenaltyAppliedIntegrationEvent>(integrationEvent =>
                    integrationEvent.PenaltyId == notification.PenaltyId &&
                    integrationEvent.ScoreEntryId == notification.ScoreEntryId &&
                    integrationEvent.LiveSessionId == notification.LiveSessionId &&
                    integrationEvent.TeamId == notification.TeamId &&
                    integrationEvent.DeductionMagnitude == notification.DeductionMagnitude &&
                    integrationEvent.Reason == notification.Reason &&
                    integrationEvent.AppliedAt == notification.AppliedAt &&
                    integrationEvent.AppliedByUserId == notification.AppliedByUserId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPublishThrows_DoesNotPropagate()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint
            .Setup(endpoint => endpoint.Publish(It.IsAny<PenaltyAppliedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unreachable"));

        var handler = new PublishPenaltyAppliedIntegrationEventHandler(
            publishEndpoint.Object,
            NullLogger<PublishPenaltyAppliedIntegrationEventHandler>.Instance);

        var act = () => handler.Handle(PenaltyAppliedEvent(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private static PenaltyApplied PenaltyAppliedEvent()
    {
        return new PenaltyApplied(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            50,
            new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero),
            Guid.NewGuid(),
            "Unsportsmanlike conduct");
    }
}
