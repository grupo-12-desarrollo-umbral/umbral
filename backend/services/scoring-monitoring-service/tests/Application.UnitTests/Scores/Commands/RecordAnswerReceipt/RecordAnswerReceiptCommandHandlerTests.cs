using Microsoft.Extensions.Logging.Abstractions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Scores.Commands.RecordAnswerReceipt;

namespace umbral_backend.Application.UnitTests.Scores.Commands.RecordAnswerReceipt;

public sealed class RecordAnswerReceiptCommandHandlerTests
{
    [Fact]
    public async Task Handle_ForwardsCommandToProbe()
    {
        var probe = new Mock<IAnswerReceiptProbe>();
        probe.Setup(p => p.Record(It.IsAny<RecordAnswerReceiptCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = new RecordAnswerReceiptCommandHandler(
            probe.Object, NullLogger<RecordAnswerReceiptCommandHandler>.Instance);
        var command = new RecordAnswerReceiptCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            QuestionSequenceOrder: 2, SelectedOptionSequenceOrder: 1,
            IsCorrect: true, ScoreValue: 15, SubmittedAt: DateTimeOffset.UtcNow);

        await handler.Handle(command, CancellationToken.None);

        probe.Verify(p => p.Record(command, It.IsAny<CancellationToken>()), Times.Once);
    }
}
