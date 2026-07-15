using MassTransit;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Scores.Common;
using umbral_backend.Application.Scores.Consumers;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.Scores.Consumers;

public sealed class LiveSessionOperatorAssignedConsumerTests
{
    [Fact]
    public async Task Consume_CallsUpsertAsync_WithCorrectValues()
    {
        var repository = new Mock<ISessionAssignmentProjectionRepository>();
        var context = new Mock<ConsumeContext<LiveSessionOperatorAssignedIntegrationEvent>>();
        var message = new LiveSessionOperatorAssignedIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
        context.SetupGet(current => current.Message).Returns(message);
        context.SetupGet(current => current.CancellationToken).Returns(CancellationToken.None);

        var consumer = new LiveSessionOperatorAssignedConsumer(repository.Object);

        await consumer.Consume(context.Object);

        repository.Verify(
            current => current.UpsertAsync(
                message.LiveSessionId,
                message.AssignedOperatorUserId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
