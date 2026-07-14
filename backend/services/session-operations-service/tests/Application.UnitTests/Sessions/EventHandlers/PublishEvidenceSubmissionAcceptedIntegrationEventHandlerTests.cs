using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.UnitTests.Sessions.EventHandlers;

public sealed class PublishEvidenceSubmissionAcceptedIntegrationEventHandlerTests
{
    [Fact]
    public async Task Handle_MapsAndPublishesAcceptedEvent()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var domainEvent = Event();
        var handler = Handler(publishEndpoint.Object);

        await handler.Handle(domainEvent, CancellationToken.None);

        publishEndpoint.Verify(endpoint => endpoint.Publish(
            It.Is<EvidenceSubmissionAcceptedIntegrationEvent>(message =>
                message.EvidenceSubmissionId == domainEvent.EvidenceSubmissionId &&
                message.ResolvedAt == domainEvent.ResolvedAt),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOutboxInsertFails_PropagatesForTransactionRollback()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint.Setup(endpoint => endpoint.Publish(
                It.IsAny<EvidenceSubmissionAcceptedIntegrationEvent>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("outbox insert failed"));

        var act = () => Handler(publishEndpoint.Object).Handle(Event(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task OutboxDispatcher_RoutesAcceptedEventToPublisher()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var acceptedHandler = Handler(publishEndpoint.Object);
        var dispatcher = CreateDispatcher(publishEndpoint.Object, acceptedHandler);
        var domainEvent = Event();

        await dispatcher.DispatchAsync(domainEvent, CancellationToken.None);

        publishEndpoint.Verify(endpoint => endpoint.Publish(
            It.Is<EvidenceSubmissionAcceptedIntegrationEvent>(message =>
                message.EvidenceSubmissionId == domainEvent.EvidenceSubmissionId),
            It.IsAny<CancellationToken>()), Times.Once);
        publishEndpoint.VerifyNoOtherCalls();
    }

    private static EvidenceSubmissionAcceptedEvent Event() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        EvidenceSubmissionType.TriviaAnswer,
        new DateTimeOffset(2026, 7, 14, 10, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 7, 14, 10, 0, 5, TimeSpan.Zero));

    private static PublishEvidenceSubmissionAcceptedIntegrationEventHandler Handler(IPublishEndpoint endpoint) =>
        new(endpoint, NullLogger<PublishEvidenceSubmissionAcceptedIntegrationEventHandler>.Instance);

    private static OutboxDomainEventDispatcher CreateDispatcher(
        IPublishEndpoint endpoint,
        PublishEvidenceSubmissionAcceptedIntegrationEventHandler acceptedHandler) =>
        new(
            new PublishAnswerRegisteredIntegrationEventHandler(
                endpoint, NullLogger<PublishAnswerRegisteredIntegrationEventHandler>.Instance),
            new PublishEvidenceSubmissionRegisteredIntegrationEventHandler(
                endpoint, NullLogger<PublishEvidenceSubmissionRegisteredIntegrationEventHandler>.Instance),
            acceptedHandler,
            new PublishEvidenceSubmissionRejectedIntegrationEventHandler(
                endpoint, NullLogger<PublishEvidenceSubmissionRejectedIntegrationEventHandler>.Instance),
            new PublishQuestionClosedIntegrationEventHandler(
                endpoint, NullLogger<PublishQuestionClosedIntegrationEventHandler>.Instance),
            new PublishSessionResultsFinalizedIntegrationEventHandler(
                endpoint, NullLogger<PublishSessionResultsFinalizedIntegrationEventHandler>.Instance),
            new PublishSessionStateChangedIntegrationEventHandler(
                endpoint, NullLogger<PublishSessionStateChangedIntegrationEventHandler>.Instance),
            new PublishTargetResolvedIntegrationEventHandler(
                endpoint, NullLogger<PublishTargetResolvedIntegrationEventHandler>.Instance));
}
