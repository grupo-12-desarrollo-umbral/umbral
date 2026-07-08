using Microsoft.Extensions.Logging.Abstractions;
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
        var publisher = new FakeIntegrationEventPublisher();
        var handler = NewHandler(publisher);

        await handler.Handle(
            new SessionStateChangedEvent(sessionId, SessionState.Active, SessionState.Finished, ChangedAt),
            CancellationToken.None);

        publisher.Published.Should().ContainSingle()
            .Which.Should().BeOfType<SessionResultsFinalizedIntegrationEvent>()
            .Which.Should().Be(new SessionResultsFinalizedIntegrationEvent(sessionId, ChangedAt));
    }

    [Theory]
    [InlineData(SessionState.Preparing)]
    [InlineData(SessionState.Active)]
    [InlineData(SessionState.Paused)]
    [InlineData(SessionState.Cancelled)]
    public async Task Handle_WhenTransitionToNonFinished_PublishesNothing(SessionState currentState)
    {
        var publisher = new FakeIntegrationEventPublisher();
        var handler = NewHandler(publisher);

        await handler.Handle(
            new SessionStateChangedEvent(Guid.NewGuid(), SessionState.Active, currentState, ChangedAt),
            CancellationToken.None);

        publisher.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenPublisherThrows_DoesNotPropagate()
    {
        var handler = NewHandler(new FakeIntegrationEventPublisher(throwOnPublish: true));

        var act = () => handler.Handle(
            new SessionStateChangedEvent(Guid.NewGuid(), SessionState.Active, SessionState.Finished, ChangedAt),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private static PublishSessionResultsFinalizedIntegrationEventHandler NewHandler(FakeIntegrationEventPublisher publisher)
        => new(publisher, NullLogger<PublishSessionResultsFinalizedIntegrationEventHandler>.Instance);
}
