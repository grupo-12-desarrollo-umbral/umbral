using MassTransit;
using MediatR;
using umbral_backend.Application.Scores.Commands.RegisterAnswerReceipt;
using umbral_backend.Application.Scores.Common;

namespace umbral_backend.Infrastructure.Messaging;

public sealed class AnswerRegisteredConsumer(
    ISender sender) : IConsumer<AnswerRegisteredIntegrationEvent>
{
    public Task Consume(ConsumeContext<AnswerRegisteredIntegrationEvent> context) =>
        sender.Send(new RegisterAnswerReceiptCommand(context.Message), context.CancellationToken);
}
