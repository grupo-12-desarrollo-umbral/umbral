using MassTransit;
using umbral_backend.Application.Rankings.Commands.RecalculateRanking;
using umbral_backend.Application.Scores.Commands.RecordScoreEntry;
using umbral_backend.Application.Scores.Common;
using umbral_backend.Application.Scores.Consumers;
using umbral_backend.Domain.Enums;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.Scores.Consumers;

public sealed class AnswerRegisteredConsumerTests
{
    [Fact]
    public async Task Consume_WhenAnswerIsCorrect_SendsRecordScoreEntryCommand()
    {
        var sender = new Mock<ISender>();
        var context = new Mock<ConsumeContext<AnswerRegisteredIntegrationEvent>>();
        var message = new AnswerRegisteredIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Gilded Owls",
            Guid.NewGuid(),
            Guid.NewGuid(),
            2,
            1,
            true,
            150,
            new DateTimeOffset(2026, 7, 14, 18, 10, 0, TimeSpan.Zero));
        context.SetupGet(current => current.Message).Returns(message);
        context.SetupGet(current => current.CancellationToken).Returns(CancellationToken.None);

        var consumer = new AnswerRegisteredConsumer(sender.Object);

        await consumer.Consume(context.Object);

        sender.Verify(
            current => current.Send(
                It.Is<RecordScoreEntryCommand>(command =>
                    command.LiveSessionId == message.LiveSessionId &&
                    command.TeamId == message.ReferenceTeamId &&
                    command.TeamDisplayName == message.TeamDisplayName &&
                    command.ScoreValue == message.ScoreValue &&
                    command.RecordedAt == message.SubmittedAt &&
                    command.SourceEntityType == ScoreSourceType.TriviaAnswerSubmission &&
                    command.SourceEntityId == message.TriviaAnswerSubmissionId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WhenAnswerIsCorrect_RecalculatesRankingSynchronously()
    {
        // The ranking is recomputed in the same consume that records the score, so the RankingChanged
        // push lands immediately instead of after a second RabbitMQ round-trip. This is what makes the
        // trivia "+points" surface on the header without depending on a possibly-missed push.
        var sender = new Mock<ISender>();
        var context = new Mock<ConsumeContext<AnswerRegisteredIntegrationEvent>>();
        var message = new AnswerRegisteredIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Gilded Owls",
            Guid.NewGuid(),
            Guid.NewGuid(),
            2,
            1,
            true,
            150,
            new DateTimeOffset(2026, 7, 14, 18, 10, 0, TimeSpan.Zero));
        context.SetupGet(current => current.Message).Returns(message);
        context.SetupGet(current => current.CancellationToken).Returns(CancellationToken.None);

        var consumer = new AnswerRegisteredConsumer(sender.Object);

        await consumer.Consume(context.Object);

        sender.Verify(
            current => current.Send(
                It.Is<RecalculateRankingCommand>(command =>
                    command.LiveSessionId == message.LiveSessionId &&
                    command.GeneratedAt == message.SubmittedAt),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WhenAnswerIsIncorrect_DoesNotSendCommand()
    {
        var sender = new Mock<ISender>();
        var context = new Mock<ConsumeContext<AnswerRegisteredIntegrationEvent>>();
        context.SetupGet(current => current.Message).Returns(
            new AnswerRegisteredIntegrationEvent(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Gilded Owls",
                Guid.NewGuid(),
                Guid.NewGuid(),
                2,
                3,
                false,
                150,
                DateTimeOffset.UtcNow));

        var consumer = new AnswerRegisteredConsumer(sender.Object);

        await consumer.Consume(context.Object);

        sender.Verify(
            current => current.Send(It.IsAny<RecordScoreEntryCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        // A wrong answer scores nothing, so it must not trigger a recalculation either.
        sender.Verify(
            current => current.Send(It.IsAny<RecalculateRankingCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
