using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.UnitTests.Sessions.EventHandlers;

public sealed class PublishEvidenceSubmissionRegisteredIntegrationEventHandlerTests
{
    [Fact]
    public async Task Handle_MapsAndPublishesGenericEvidenceRegistration()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var domainEvent = Event();
        var handler = Handler(publishEndpoint.Object);

        await handler.Handle(domainEvent, CancellationToken.None);

        publishEndpoint.Verify(endpoint => endpoint.Publish(
            new EvidenceSubmissionRegisteredIntegrationEvent(
                domainEvent.LiveSessionId,
                domainEvent.TeamId,
                domainEvent.EvidenceSubmissionId,
                domainEvent.ActiveSubstageId,
                domainEvent.SubmissionType,
                domainEvent.SubmittedAt,
                domainEvent.ValidationState),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOutboxInsertFails_PropagatesForTransactionRollback()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint.Setup(endpoint => endpoint.Publish(
                It.IsAny<EvidenceSubmissionRegisteredIntegrationEvent>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("outbox insert failed"));

        var act = () => Handler(publishEndpoint.Object).Handle(Event(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task OutboxDispatcher_RoutesEvidenceRegistrationToPublisher()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var evidenceHandler = Handler(publishEndpoint.Object);
        var dispatcher = new OutboxDomainEventDispatcher(
            new PublishAnswerRegisteredIntegrationEventHandler(
                publishEndpoint.Object,
                NullLogger<PublishAnswerRegisteredIntegrationEventHandler>.Instance),
            evidenceHandler,
            new PublishEvidenceSubmissionAcceptedIntegrationEventHandler(
                publishEndpoint.Object,
                NullLogger<PublishEvidenceSubmissionAcceptedIntegrationEventHandler>.Instance),
            new PublishEvidenceSubmissionRejectedIntegrationEventHandler(
                publishEndpoint.Object,
                NullLogger<PublishEvidenceSubmissionRejectedIntegrationEventHandler>.Instance),
            new PublishQuestionClosedIntegrationEventHandler(
                publishEndpoint.Object,
                NullLogger<PublishQuestionClosedIntegrationEventHandler>.Instance),
            new PublishSessionResultsFinalizedIntegrationEventHandler(
                publishEndpoint.Object,
                NullLogger<PublishSessionResultsFinalizedIntegrationEventHandler>.Instance),
            new PublishSessionStateChangedIntegrationEventHandler(
                publishEndpoint.Object,
                NullLogger<PublishSessionStateChangedIntegrationEventHandler>.Instance),
            new PublishTargetResolvedIntegrationEventHandler(
                publishEndpoint.Object,
                NullLogger<PublishTargetResolvedIntegrationEventHandler>.Instance),
            new PublishLiveSessionOperatorAssignedIntegrationEventHandler(
                publishEndpoint.Object,
                NullLogger<PublishLiveSessionOperatorAssignedIntegrationEventHandler>.Instance));
        var domainEvent = Event();

        await dispatcher.DispatchAsync(domainEvent, CancellationToken.None);

        publishEndpoint.Verify(endpoint => endpoint.Publish(
            It.Is<EvidenceSubmissionRegisteredIntegrationEvent>(message =>
                message.EvidenceSubmissionId == domainEvent.EvidenceSubmissionId),
            It.IsAny<CancellationToken>()), Times.Once);
        publishEndpoint.VerifyNoOtherCalls();
    }

    private static EvidenceSubmissionRegisteredEvent Event() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        EvidenceSubmissionType.TriviaAnswer,
        new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero),
        EvidenceValidationState.Pending);

    private static PublishEvidenceSubmissionRegisteredIntegrationEventHandler Handler(IPublishEndpoint endpoint) =>
        new(endpoint, NullLogger<PublishEvidenceSubmissionRegisteredIntegrationEventHandler>.Instance);
}
