using umbral_backend.Application.Scores.Common;

namespace umbral_backend.Application.Common.Interfaces;

public interface IAnswerReceiptTestSeam
{
    Task ReceivedAsync(AnswerRegisteredIntegrationEvent answer, CancellationToken cancellationToken);
}
