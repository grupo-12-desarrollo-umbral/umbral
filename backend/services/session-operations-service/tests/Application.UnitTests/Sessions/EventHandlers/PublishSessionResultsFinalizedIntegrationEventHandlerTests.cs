using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.UnitTests.Sessions.EventHandlers;

// Locks the SessionStateChangedEvent(->Finished) -> SessionResultsFinalizedIntegrationEvent bridge
// (HU-33B X.2): exactly one mapped event on Finished, nothing on any non-Finished target state,
// correlation-only fields (no score, D-1), and a throwing publisher never propagates (D-3).
public sealed class PublishSessionResultsFinalizedIntegrationEventHandlerTests
{
    private static readonly DateTimeOffset ChangedAt = new(2026, 7, 8, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenTransitionToFinished_PublishesExactlyOneSessionResultsFinalizedIntegrationEvent()
    {
        var sessionId = Guid.NewGuid();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var handler = NewHandler(publishEndpoint.Object);

        await handler.Handle(
            new SessionStateChangedEvent(sessionId, SessionState.Active, SessionState.Finished, ChangedAt),
            CancellationToken.None);

        publishEndpoint.Verify(
            endpoint => endpoint.Publish(
                new SessionResultsFinalizedIntegrationEvent(sessionId, ChangedAt),
                It.IsAny<CancellationToken>()),
            Times.Once);
        publishEndpoint.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(SessionState.Preparing)]
    [InlineData(SessionState.Active)]
    [InlineData(SessionState.Paused)]
    [InlineData(SessionState.Cancelled)]
    public async Task Handle_WhenTransitionToNonFinished_PublishesNothing(SessionState currentState)
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var handler = NewHandler(publishEndpoint.Object);

        await handler.Handle(
            new SessionStateChangedEvent(Guid.NewGuid(), SessionState.Active, currentState, ChangedAt),
            CancellationToken.None);

        publishEndpoint.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenPublisherThrows_DoesNotPropagate()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint
            .Setup(endpoint => endpoint.Publish(
                It.IsAny<SessionResultsFinalizedIntegrationEvent>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unreachable"));
        var handler = NewHandler(publishEndpoint.Object);

        var act = () => handler.Handle(
            new SessionStateChangedEvent(Guid.NewGuid(), SessionState.Active, SessionState.Finished, ChangedAt),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private static PublishSessionResultsFinalizedIntegrationEventHandler NewHandler(IPublishEndpoint publishEndpoint)
        => new(publishEndpoint, NullLogger<PublishSessionResultsFinalizedIntegrationEventHandler>.Instance);
}
