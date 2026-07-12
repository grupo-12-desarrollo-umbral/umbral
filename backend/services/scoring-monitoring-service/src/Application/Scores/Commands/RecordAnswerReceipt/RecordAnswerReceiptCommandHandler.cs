using Microsoft.Extensions.Logging;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Application.Scores.Commands.RecordAnswerReceipt;

/// <summary>
/// Thin handler for the first HU-37 slice (#191): it logs receipt of an accepted answer and calls
/// the <see cref="IAnswerReceiptProbe"/> seam. It deliberately writes no <c>ScoreEntry</c> — the
/// scoring aggregate arrives in HU-37 — so this proves the transport path without business logic.
/// </summary>
public sealed class RecordAnswerReceiptCommandHandler : IRequestHandler<RecordAnswerReceiptCommand>
{
    private readonly IAnswerReceiptProbe _probe;
    private readonly ILogger<RecordAnswerReceiptCommandHandler> _logger;

    public RecordAnswerReceiptCommandHandler(
        IAnswerReceiptProbe probe,
        ILogger<RecordAnswerReceiptCommandHandler> logger)
    {
        _probe = probe;
        _logger = logger;
    }

    public async Task Handle(RecordAnswerReceiptCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Recorded answer receipt for session {LiveSessionId} team {TeamId} question {QuestionSequenceOrder} (correct={IsCorrect}, score={ScoreValue}).",
            request.LiveSessionId,
            request.TeamId,
            request.QuestionSequenceOrder,
            request.IsCorrect,
            request.ScoreValue);

        await _probe.Record(request, cancellationToken);
    }
}
