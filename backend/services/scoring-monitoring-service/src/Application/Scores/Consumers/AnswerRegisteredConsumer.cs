using MassTransit;
using umbral_backend.Application.Scores.Commands.RecordScoreEntry;
using umbral_backend.Application.Scores.Common;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Scores.Consumers;

public sealed class AnswerRegisteredConsumer : IConsumer<AnswerRegisteredIntegrationEvent>
{
    private const string TriviaAnswerCorrectReasonCode = "trivia-answer-correct";

    private readonly ISender _sender;

    public AnswerRegisteredConsumer(ISender sender)
    {
        _sender = sender;
    }

    public async Task Consume(ConsumeContext<AnswerRegisteredIntegrationEvent> context)
    {
        if (!context.Message.IsCorrect)
        {
            return;
        }

        await _sender.Send(
            new RecordScoreEntryCommand(
                context.Message.LiveSessionId,
                // Key scoring/ranking on the cross-context ReferenceTeamId (what mobile + seed use),
                // not the session-scoped TeamId — otherwise the score lands on a phantom team row.
                context.Message.ReferenceTeamId,
                context.Message.TeamDisplayName,
                TriviaAnswerCorrectReasonCode,
                context.Message.ScoreValue,
                context.Message.SubmittedAt,
                ScoreSourceType.TriviaAnswerSubmission,
                context.Message.TriviaAnswerSubmissionId),
            context.CancellationToken);
    }
}
