using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Scores.Common;

namespace umbral_backend.Infrastructure.Messaging;

public sealed class NullAnswerReceiptTestSeam : IAnswerReceiptTestSeam
{
    public Task ReceivedAsync(
        AnswerRegisteredIntegrationEvent answer,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
