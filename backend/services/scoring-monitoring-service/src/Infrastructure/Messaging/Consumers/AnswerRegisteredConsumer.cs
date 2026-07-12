using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Scores.Commands.RecordAnswerReceipt;
using umbral_backend.Application.Scores.Common;

namespace umbral_backend.Infrastructure.Messaging.Consumers;

/// <summary>
/// Thin transport adapter (ADR-0017): it maps the inbound
/// <see cref="AnswerRegisteredIntegrationEvent"/> to a <see cref="RecordAnswerReceiptCommand"/> and
/// dispatches it through MediatR. No scoring business logic lives here — the consumer never touches
/// a <c>ScoreEntry</c> or the database directly; all in-process work happens in the handler.
/// </summary>
public sealed class AnswerRegisteredConsumer : IConsumer<AnswerRegisteredIntegrationEvent>
{
    private readonly ISender _sender;
    private readonly ILogger<AnswerRegisteredConsumer> _logger;

    public AnswerRegisteredConsumer(ISender sender, ILogger<AnswerRegisteredConsumer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AnswerRegisteredIntegrationEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Consumed AnswerRegisteredIntegrationEvent for session {LiveSessionId} team {TeamId} question {QuestionSequenceOrder}.",
            message.LiveSessionId,
            message.TeamId,
            message.QuestionSequenceOrder);

        await _sender.Send(
            new RecordAnswerReceiptCommand(
                message.LiveSessionId,
                message.TeamId,
                message.TriviaAnswerSubmissionId,
                message.TriviaSubstageSnapshotId,
                message.QuestionSequenceOrder,
                message.SelectedOptionSequenceOrder,
                message.IsCorrect,
                message.ScoreValue,
                message.SubmittedAt),
            context.CancellationToken);
    }
}
