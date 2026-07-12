using MediatR;
using umbral_backend.Application.Scores.Common;

namespace umbral_backend.Application.Scores.Commands.RegisterAnswerReceipt;

public sealed record RegisterAnswerReceiptCommand(
    AnswerRegisteredIntegrationEvent Answer) : IRequest;
