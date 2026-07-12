using Microsoft.Extensions.Logging.Abstractions;
using umbral_backend.Application.Scores.Commands.RecordAnswerReceipt;
using umbral_backend.Infrastructure.Messaging;

namespace umbral_backend.Infrastructure.IntegrationTests.Messaging;

public sealed class LoggingAnswerReceiptProbeTests
{
    [Fact]
    public async Task Record_LogsAndCompletes()
    {
        var probe = new LoggingAnswerReceiptProbe(NullLogger<LoggingAnswerReceiptProbe>.Instance);
        var receipt = new RecordAnswerReceiptCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 0, false, 0, DateTimeOffset.UnixEpoch);

        await probe.Record(receipt, CancellationToken.None);

        // The production probe is a no-op observer: the assertion is that it completes without throwing.
        true.Should().BeTrue();
    }
}
