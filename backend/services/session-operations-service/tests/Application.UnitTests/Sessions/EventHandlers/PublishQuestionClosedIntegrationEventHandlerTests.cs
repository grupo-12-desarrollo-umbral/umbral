using Microsoft.Extensions.Logging.Abstractions;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.UnitTests.Sessions.EventHandlers;

// Locks the QuestionClosedEvent -> QuestionClosedIntegrationEvent bridge (HU-33B X.2): exactly one
// mapped event, correlation-only fields (no score, D-1), and a throwing publisher never propagates (D-3).
public sealed class PublishQuestionClosedIntegrationEventHandlerTests
{
    private static readonly DateTimeOffset ClosedAt = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_PublishesExactlyOneQuestionClosedIntegrationEvent()
    {
        var sessionId = Guid.NewGuid();
        var publisher = new FakeIntegrationEventPublisher();
        var handler = new PublishQuestionClosedIntegrationEventHandler(
            publisher,
            NullLogger<PublishQuestionClosedIntegrationEventHandler>.Instance);

        await handler.Handle(
            new QuestionClosedEvent(sessionId, questionIndex: 2, ClosedAt, wasExpiredByTimer: true),
            CancellationToken.None);

        publisher.Published.Should().ContainSingle()
            .Which.Should().BeOfType<QuestionClosedIntegrationEvent>()
            .Which.Should().Be(new QuestionClosedIntegrationEvent(sessionId, 2, ClosedAt));
    }

    [Fact]
    public async Task Handle_WhenPublisherThrows_DoesNotPropagate()
    {
        var handler = new PublishQuestionClosedIntegrationEventHandler(
            new FakeIntegrationEventPublisher(throwOnPublish: true),
            NullLogger<PublishQuestionClosedIntegrationEventHandler>.Instance);

        var act = () => handler.Handle(
            new QuestionClosedEvent(Guid.NewGuid(), questionIndex: 0, ClosedAt, wasExpiredByTimer: false),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
