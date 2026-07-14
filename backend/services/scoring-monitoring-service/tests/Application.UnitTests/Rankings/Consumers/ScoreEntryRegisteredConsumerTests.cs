using MassTransit;
using umbral_backend.Application.Rankings.Commands.RecalculateRanking;
using umbral_backend.Application.Rankings.Consumers;
using umbral_backend.Application.Scores.Common;
using umbral_backend.Domain.Enums;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.Rankings.Consumers;

public sealed class ScoreEntryRegisteredConsumerTests
{
    [Fact]
    public async Task Consume_SendsRecalculateRankingCommand()
    {
        var sender = new Mock<ISender>();
        var context = new Mock<ConsumeContext<ScoreEntryRegisteredIntegrationEvent>>();
        var message = new ScoreEntryRegisteredIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ScoreEntryType.Grant,
            "trivia-answer-correct",
            100,
            new DateTimeOffset(2026, 7, 14, 18, 40, 0, TimeSpan.Zero),
            ScoreSourceType.TriviaAnswerSubmission,
            Guid.NewGuid(),
            null);
        context.SetupGet(current => current.Message).Returns(message);
        context.SetupGet(current => current.CancellationToken).Returns(CancellationToken.None);

        var consumer = new ScoreEntryRegisteredConsumer(sender.Object);

        await consumer.Consume(context.Object);

        sender.Verify(
            current => current.Send(
                It.Is<RecalculateRankingCommand>(command =>
                    command.LiveSessionId == message.LiveSessionId &&
                    command.GeneratedAt == message.RecordedAt),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
