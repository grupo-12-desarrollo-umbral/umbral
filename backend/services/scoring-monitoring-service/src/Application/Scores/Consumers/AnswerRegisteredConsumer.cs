using MassTransit;
using umbral_backend.Application.Rankings.Commands.RecalculateRanking;
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

        // Recalculate the ranking here, in the same consume that recorded the score, so the
        // RankingChanged push reaches participants immediately instead of waiting on a second
        // RabbitMQ round-trip (ScoreEntryRegistered → outbox → ScoreEntryRegisteredConsumer). That
        // async path still runs as a self-healing fallback, and re-running the recalc is safe:
        // recording is idempotent (ExistsForSourceAsync) and Ranking.Refresh folds the whole ledger,
        // so a duplicate recalculation only re-derives the same snapshot. It also self-heals a lost
        // recalc — a failure fails this consume and the redelivery re-runs the recalc even though the
        // already-recorded score entry is deduped. Without this the trivia "+points" never surfaced,
        // because the trivia flow had no client-side ranking catch-up to cover a missed push.
        await _sender.Send(
            new RecalculateRankingCommand(
                context.Message.LiveSessionId,
                context.Message.SubmittedAt),
            context.CancellationToken);
    }
}
