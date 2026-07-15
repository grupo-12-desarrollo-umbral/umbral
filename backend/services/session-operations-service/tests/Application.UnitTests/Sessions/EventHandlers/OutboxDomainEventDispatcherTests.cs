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
            new PublishEvidenceSubmissionRegisteredIntegrationEventHandler(
                publishEndpoint.Object,
                NullLogger<PublishEvidenceSubmissionRegisteredIntegrationEventHandler>.Instance),
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

    [Fact]
    public async Task DispatchAsync_ForOperatorAssignedWithExternalId_PublishesAuthorizationProjectionEvent()
    {
        var liveSessionId = Guid.NewGuid();
        var operatorSub = Guid.NewGuid();
        var published = new List<LiveSessionOperatorAssignedIntegrationEvent>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint
            .Setup(endpoint => endpoint.Publish(
                It.IsAny<LiveSessionOperatorAssignedIntegrationEvent>(),
                It.IsAny<CancellationToken>()))
            .Callback<LiveSessionOperatorAssignedIntegrationEvent, CancellationToken>((message, _) => published.Add(message))
            .Returns(Task.CompletedTask);

        var dispatcher = CreateDispatcher(publishEndpoint.Object);
        var assignedAt = new DateTimeOffset(2026, 7, 15, 18, 10, 0, TimeSpan.Zero);

        await dispatcher.DispatchAsync(
            new LiveSessionOperatorAssignedEvent(
                liveSessionId,
                previousOperatorUserId: null,
                assignedOperatorUserId: 27,
                assignedOperatorExternalId: operatorSub.ToString(),
                assignedAt),
            CancellationToken.None);

        published.Should().ContainSingle().Which.Should().Be(
            new LiveSessionOperatorAssignedIntegrationEvent(liveSessionId, operatorSub, assignedAt));
    }

    [Fact]
    public async Task DispatchAsync_ForOperatorAssignedWithoutResolvableExternalId_PublishesNothing()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var dispatcher = CreateDispatcher(publishEndpoint.Object);

        await dispatcher.DispatchAsync(
            new LiveSessionOperatorAssignedEvent(
                Guid.NewGuid(),
                previousOperatorUserId: null,
                assignedOperatorUserId: 27,
                assignedOperatorExternalId: null,
                DateTimeOffset.UtcNow),
            CancellationToken.None);

        publishEndpoint.Verify(
            endpoint => endpoint.Publish(
                It.IsAny<LiveSessionOperatorAssignedIntegrationEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static OutboxDomainEventDispatcher CreateDispatcher(IPublishEndpoint publishEndpoint) =>
        new(
            new PublishAnswerRegisteredIntegrationEventHandler(
                publishEndpoint,
                NullLogger<PublishAnswerRegisteredIntegrationEventHandler>.Instance),
            new PublishEvidenceSubmissionRegisteredIntegrationEventHandler(
                publishEndpoint,
                NullLogger<PublishEvidenceSubmissionRegisteredIntegrationEventHandler>.Instance),
            new PublishEvidenceSubmissionAcceptedIntegrationEventHandler(
                publishEndpoint,
                NullLogger<PublishEvidenceSubmissionAcceptedIntegrationEventHandler>.Instance),
            new PublishEvidenceSubmissionRejectedIntegrationEventHandler(
                publishEndpoint,
                NullLogger<PublishEvidenceSubmissionRejectedIntegrationEventHandler>.Instance),
            new PublishQuestionClosedIntegrationEventHandler(
                publishEndpoint,
                NullLogger<PublishQuestionClosedIntegrationEventHandler>.Instance),
            new PublishSessionResultsFinalizedIntegrationEventHandler(
                publishEndpoint,
                NullLogger<PublishSessionResultsFinalizedIntegrationEventHandler>.Instance),
            new PublishSessionStateChangedIntegrationEventHandler(
                publishEndpoint,
                NullLogger<PublishSessionStateChangedIntegrationEventHandler>.Instance),
            new PublishTargetResolvedIntegrationEventHandler(
                publishEndpoint,
                NullLogger<PublishTargetResolvedIntegrationEventHandler>.Instance),
            new PublishLiveSessionOperatorAssignedIntegrationEventHandler(
                publishEndpoint,
                NullLogger<PublishLiveSessionOperatorAssignedIntegrationEventHandler>.Instance));
}
