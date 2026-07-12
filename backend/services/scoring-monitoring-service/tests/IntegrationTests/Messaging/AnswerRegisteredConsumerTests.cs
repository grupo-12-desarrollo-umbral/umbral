using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using umbral_backend.Application.Scores.Commands.RecordAnswerReceipt;
using umbral_backend.Application.Scores.Common;
using umbral_backend.Infrastructure.Messaging.Consumers;

namespace umbral_backend.Infrastructure.IntegrationTests.Messaging;

// Broker-free proof that the consumer is a thin transport adapter (ADR-0017): it maps the inbound
// integration event to a RecordAnswerReceiptCommand and dispatches it through ISender — no scoring
// logic of its own.
public sealed class AnswerRegisteredConsumerTests
{
    [Fact]
    public async Task Consume_MapsIntegrationEventToCommandAndDispatches()
    {
        var message = new AnswerRegisteredIntegrationEvent(
            LiveSessionId: Guid.NewGuid(),
            TeamId: Guid.NewGuid(),
            TriviaAnswerSubmissionId: Guid.NewGuid(),
            TriviaSubstageSnapshotId: Guid.NewGuid(),
            QuestionSequenceOrder: 4,
            SelectedOptionSequenceOrder: 2,
            IsCorrect: true,
            ScoreValue: 25,
            SubmittedAt: DateTimeOffset.UnixEpoch);

        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<RecordAnswerReceiptCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var context = new Mock<ConsumeContext<AnswerRegisteredIntegrationEvent>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        var consumer = new AnswerRegisteredConsumer(sender.Object, NullLogger<AnswerRegisteredConsumer>.Instance);

        await consumer.Consume(context.Object);

        sender.Verify(
            s => s.Send(
                It.Is<RecordAnswerReceiptCommand>(command =>
                    command.LiveSessionId == message.LiveSessionId &&
                    command.TeamId == message.TeamId &&
                    command.TriviaAnswerSubmissionId == message.TriviaAnswerSubmissionId &&
                    command.TriviaSubstageSnapshotId == message.TriviaSubstageSnapshotId &&
                    command.QuestionSequenceOrder == message.QuestionSequenceOrder &&
                    command.SelectedOptionSequenceOrder == message.SelectedOptionSequenceOrder &&
                    command.IsCorrect == message.IsCorrect &&
                    command.ScoreValue == message.ScoreValue &&
                    command.SubmittedAt == message.SubmittedAt),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
