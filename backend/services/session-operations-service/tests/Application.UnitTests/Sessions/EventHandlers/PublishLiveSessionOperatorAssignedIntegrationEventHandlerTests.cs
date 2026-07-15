using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.UnitTests.Sessions.EventHandlers;

public sealed class PublishLiveSessionOperatorAssignedIntegrationEventHandlerTests
{
    [Fact]
    public async Task Handle_MapsInternalAssignmentOntoTheSubKeyedAuthorizationContract()
    {
        var liveSessionId = Guid.NewGuid();
        var operatorSub = Guid.NewGuid();
        var assignedAt = new DateTimeOffset(2026, 7, 15, 18, 10, 0, TimeSpan.Zero);
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var handler = Handler(publishEndpoint.Object);

        await handler.Handle(Event(liveSessionId, operatorSub.ToString(), assignedAt), CancellationToken.None);

        publishEndpoint.Verify(endpoint => endpoint.Publish(
            new LiveSessionOperatorAssignedIntegrationEvent(liveSessionId, operatorSub, assignedAt),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task Handle_WhenExternalIdentityIsNotAResolvableGuid_PublishesNothing(string? externalId)
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();

        await Handler(publishEndpoint.Object)
            .Handle(Event(Guid.NewGuid(), externalId, DateTimeOffset.UtcNow), CancellationToken.None);

        publishEndpoint.Verify(endpoint => endpoint.Publish(
            It.IsAny<LiveSessionOperatorAssignedIntegrationEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOutboxInsertFails_PropagatesForTransactionRollback()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint.Setup(endpoint => endpoint.Publish(
                It.IsAny<LiveSessionOperatorAssignedIntegrationEvent>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("outbox insert failed"));

        var act = () => Handler(publishEndpoint.Object)
            .Handle(Event(Guid.NewGuid(), Guid.NewGuid().ToString(), DateTimeOffset.UtcNow), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static LiveSessionOperatorAssignedEvent Event(Guid liveSessionId, string? externalId, DateTimeOffset assignedAt) =>
        new(
            liveSessionId,
            previousOperatorUserId: null,
            assignedOperatorUserId: 27,
            assignedOperatorExternalId: externalId,
            assignedAt);

    private static PublishLiveSessionOperatorAssignedIntegrationEventHandler Handler(IPublishEndpoint endpoint) =>
        new(endpoint, NullLogger<PublishLiveSessionOperatorAssignedIntegrationEventHandler>.Instance);
}
