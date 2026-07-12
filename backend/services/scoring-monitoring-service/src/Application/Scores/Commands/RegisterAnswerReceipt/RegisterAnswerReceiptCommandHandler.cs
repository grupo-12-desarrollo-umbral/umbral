using MediatR;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Application.Scores.Commands.RegisterAnswerReceipt;

public sealed class RegisterAnswerReceiptCommandHandler(
    ILogger<RegisterAnswerReceiptCommandHandler> logger,
    IAnswerReceiptTestSeam testSeam) : IRequestHandler<RegisterAnswerReceiptCommand>
{
    public async Task Handle(RegisterAnswerReceiptCommand request, CancellationToken cancellationToken)
    {
        var answer = request.Answer;
        logger.LogInformation(
            "Received AnswerRegisteredIntegrationEvent for session {LiveSessionId}, team {TeamId}, question {QuestionSequenceOrder}.",
            answer.LiveSessionId,
            answer.TeamId,
            answer.QuestionSequenceOrder);

        await testSeam.ReceivedAsync(answer, cancellationToken);
    }
}
