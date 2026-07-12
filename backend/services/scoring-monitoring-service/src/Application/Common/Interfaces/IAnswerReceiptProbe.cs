using umbral_backend.Application.Scores.Commands.RecordAnswerReceipt;

namespace umbral_backend.Application.Common.Interfaces;

/// <summary>
/// Observation seam for a recorded answer receipt. The production implementation is a logging
/// no-op; the messaging integration test swaps in a <see cref="TaskCompletionSource{TResult}"/>
/// fake so it can assert the broker → consumer → handler path ran end-to-end without a real
/// <c>ScoreEntry</c> write existing yet (HU-37).
/// </summary>
public interface IAnswerReceiptProbe
{
    Task Record(RecordAnswerReceiptCommand receipt, CancellationToken cancellationToken);
}
