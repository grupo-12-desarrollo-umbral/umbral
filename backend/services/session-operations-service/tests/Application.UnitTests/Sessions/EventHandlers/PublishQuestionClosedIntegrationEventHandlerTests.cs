using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.UnitTests.Sessions.EventHandlers;

// Locks the QuestionClosedEvent -> QuestionClosedIntegrationEvent bridge (#164): exactly one event
// published through MassTransit's IPublishEndpoint, correlation-only fields (no score, D-1), and a
// throwing publish never propagates into the dispatch (D-3).
public sealed class PublishQuestionClosedIntegrationEventHandlerTests
{
    private static readonly DateTimeOffset ClosedAt = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_PublishesExactlyOneQuestionClosedIntegrationEvent()
    {
        var sessionId = Guid.NewGuid();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var handler = new PublishQuestionClosedIntegrationEventHandler(
            publishEndpoint.Object,
            NullLogger<PublishQuestionClosedIntegrationEventHandler>.Instance);

        await handler.Handle(
            new QuestionClosedEvent(sessionId, questionIndex: 2, ClosedAt, wasExpiredByTimer: true),
            CancellationToken.None);

        publishEndpoint.Verify(
            endpoint => endpoint.Publish(
                new QuestionClosedIntegrationEvent(sessionId, 2, ClosedAt),
                It.IsAny<CancellationToken>()),
            Times.Once);
        publishEndpoint.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenPublishThrows_DoesNotPropagate()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint
            .Setup(endpoint => endpoint.Publish(It.IsAny<QuestionClosedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unreachable"));
        var handler = new PublishQuestionClosedIntegrationEventHandler(
            publishEndpoint.Object,
            NullLogger<PublishQuestionClosedIntegrationEventHandler>.Instance);

        var act = () => handler.Handle(
            new QuestionClosedEvent(Guid.NewGuid(), questionIndex: 0, ClosedAt, wasExpiredByTimer: false),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
