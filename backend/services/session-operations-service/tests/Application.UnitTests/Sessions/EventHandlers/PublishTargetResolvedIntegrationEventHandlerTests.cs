using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.UnitTests.Sessions.EventHandlers;

public sealed class PublishTargetResolvedIntegrationEventHandlerTests
{
    [Fact]
    public async Task Handle_PublishesMappedTargetResolvedFact()
    {
        var endpoint = new Mock<IPublishEndpoint>();
        var handler = new PublishTargetResolvedIntegrationEventHandler(
            endpoint.Object,
            NullLogger<PublishTargetResolvedIntegrationEventHandler>.Instance);
        var domainEvent = new TargetResolvedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150,
            new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero));

        await handler.Handle(domainEvent, CancellationToken.None);

        endpoint.Verify(publisher => publisher.Publish(
            new TargetResolvedIntegrationEvent(
                domainEvent.LiveSessionId, domainEvent.TeamId, domainEvent.EvidenceSubmissionId,
                domainEvent.ActiveSubstageId, domainEvent.TargetSnapshotId, 150, domainEvent.ResolvedAt),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOutboxInsertFails_Propagates()
    {
        var endpoint = new Mock<IPublishEndpoint>();
        endpoint.Setup(publisher => publisher.Publish(
                It.IsAny<TargetResolvedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("outbox insert failed"));
        var handler = new PublishTargetResolvedIntegrationEventHandler(
            endpoint.Object,
            NullLogger<PublishTargetResolvedIntegrationEventHandler>.Instance);
        var domainEvent = new TargetResolvedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100,
            DateTimeOffset.UtcNow);

        await FluentActions.Awaiting(() => handler.Handle(domainEvent, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }
}
