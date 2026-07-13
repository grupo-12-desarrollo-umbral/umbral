using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.UnitTests.Sessions.EventHandlers;

public sealed class OutboxDomainEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_ForFinishedTransition_AwaitsResultsThenAuditPublisher()
    {
        var publicationOrder = new List<Type>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint
            .Setup(endpoint => endpoint.Publish(
                It.IsAny<SessionResultsFinalizedIntegrationEvent>(),
                It.IsAny<CancellationToken>()))
            .Callback<SessionResultsFinalizedIntegrationEvent, CancellationToken>(
                (_, _) => publicationOrder.Add(typeof(SessionResultsFinalizedIntegrationEvent)))
            .Returns(Task.CompletedTask);
        publishEndpoint
            .Setup(endpoint => endpoint.Publish(
                It.IsAny<SessionStateChangedIntegrationEvent>(),
                It.IsAny<CancellationToken>()))
            .Callback<SessionStateChangedIntegrationEvent, CancellationToken>(
                (_, _) => publicationOrder.Add(typeof(SessionStateChangedIntegrationEvent)))
            .Returns(Task.CompletedTask);

        var dispatcher = new OutboxDomainEventDispatcher(
            new PublishAnswerRegisteredIntegrationEventHandler(
                publishEndpoint.Object,
                NullLogger<PublishAnswerRegisteredIntegrationEventHandler>.Instance),
            new PublishQuestionClosedIntegrationEventHandler(
                publishEndpoint.Object,
                NullLogger<PublishQuestionClosedIntegrationEventHandler>.Instance),
            new PublishSessionResultsFinalizedIntegrationEventHandler(
                publishEndpoint.Object,
                NullLogger<PublishSessionResultsFinalizedIntegrationEventHandler>.Instance),
            new PublishSessionStateChangedIntegrationEventHandler(
                publishEndpoint.Object,
                NullLogger<PublishSessionStateChangedIntegrationEventHandler>.Instance));

        await dispatcher.DispatchAsync(
            new SessionStateChangedEvent(
                Guid.NewGuid(),
                SessionState.Active,
                SessionState.Finished,
                DateTimeOffset.UtcNow,
                responsibleUserId: 42,
                actorType: SessionEventActorType.Operator),
            CancellationToken.None);

        publicationOrder.Should().Equal(
            typeof(SessionResultsFinalizedIntegrationEvent),
            typeof(SessionStateChangedIntegrationEvent));
    }
}
