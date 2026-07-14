using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.UnitTests.Sessions.EventHandlers;

public sealed class PublishSessionStateChangedIntegrationEventHandlerTests
{
    private static readonly DateTimeOffset ChangedAt = new(2026, 7, 13, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_PublishesExactlyOneMappedSessionStateChangedIntegrationEvent()
    {
        var sessionId = Guid.NewGuid();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var handler = NewHandler(publishEndpoint.Object);

        await handler.Handle(
            new SessionStateChangedEvent(
                sessionId,
                SessionState.Scheduled,
                SessionState.Preparing,
                ChangedAt,
                responsibleUserId: 42,
                reason: "Doors open",
                actorType: SessionEventActorType.Operator),
            CancellationToken.None);

        publishEndpoint.Verify(
            endpoint => endpoint.Publish(
                new SessionStateChangedIntegrationEvent(
                    sessionId,
                    SessionState.Scheduled,
                    SessionState.Preparing,
                    ChangedAt,
                    42,
                    "Doors open"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        publishEndpoint.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenOutboxInsertFails_PropagatesToRollBackTheTransaction()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint
            .Setup(endpoint => endpoint.Publish(
                It.IsAny<SessionStateChangedIntegrationEvent>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("outbox insert failed"));
        var handler = NewHandler(publishEndpoint.Object);

        var act = () => handler.Handle(
            new SessionStateChangedEvent(
                Guid.NewGuid(),
                SessionState.Active,
                SessionState.Paused,
                ChangedAt,
                responsibleUserId: 42,
                reason: "Break",
                actorType: SessionEventActorType.Operator),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static PublishSessionStateChangedIntegrationEventHandler NewHandler(IPublishEndpoint publishEndpoint)
        => new(publishEndpoint, NullLogger<PublishSessionStateChangedIntegrationEventHandler>.Instance);
}
