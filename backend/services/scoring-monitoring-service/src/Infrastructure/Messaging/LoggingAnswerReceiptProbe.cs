using Microsoft.Extensions.Logging;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Scores.Commands.RecordAnswerReceipt;

namespace umbral_backend.Infrastructure.Messaging;

/// <summary>
/// Production <see cref="IAnswerReceiptProbe"/>: a logging no-op. The receipt has already been
/// logged by the handler, so this exists only to make the seam concrete until HU-37 replaces it
/// with a real <c>ScoreEntry</c> write. The messaging integration test substitutes a
/// <see cref="TaskCompletionSource{TResult}"/> fake in its place.
/// </summary>
public sealed class LoggingAnswerReceiptProbe : IAnswerReceiptProbe
{
    private readonly ILogger<LoggingAnswerReceiptProbe> _logger;

    public LoggingAnswerReceiptProbe(ILogger<LoggingAnswerReceiptProbe> logger)
    {
        _logger = logger;
    }

    public Task Record(RecordAnswerReceiptCommand receipt, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Answer receipt observed for session {LiveSessionId} team {TeamId}.",
            receipt.LiveSessionId,
            receipt.TeamId);

        return Task.CompletedTask;
    }
}
