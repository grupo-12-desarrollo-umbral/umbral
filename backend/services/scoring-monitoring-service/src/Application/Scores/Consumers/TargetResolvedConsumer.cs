using MassTransit;
using umbral_backend.Application.Rankings.Commands.RecalculateRanking;
using umbral_backend.Application.Scores.Commands.RecordScoreEntry;
using umbral_backend.Application.Scores.Common;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Scores.Consumers;

public sealed class TargetResolvedConsumer : IConsumer<TargetResolvedIntegrationEvent>
{
    private const string TreasureTargetResolvedReasonCode = "treasure-target-resolved";

    private readonly ISender _sender;

    public TargetResolvedConsumer(ISender sender)
    {
        _sender = sender;
    }

    public async Task Consume(ConsumeContext<TargetResolvedIntegrationEvent> context)
    {
        await _sender.Send(
            new RecordScoreEntryCommand(
                context.Message.LiveSessionId,
                // Key scoring/ranking on the cross-context ReferenceTeamId (what mobile + seed use),
                // not the session-scoped TeamId — otherwise the score lands on a phantom team row.
                context.Message.ReferenceTeamId,
                context.Message.TeamDisplayName,
                TreasureTargetResolvedReasonCode,
                context.Message.ScoreValue,
                context.Message.ResolvedAt,
                ScoreSourceType.TargetResolution,
                context.Message.EvidenceSubmissionId,
                DifficultyFactor: context.Message.DifficultyFactor),
            context.CancellationToken);

        // Recalculate the ranking here, in the same consume that recorded the score, so the
        // RankingChanged push reaches participants immediately instead of waiting on a second
        // RabbitMQ round-trip (ScoreEntryRegistered → outbox → ScoreEntryRegisteredConsumer). That
        // async path still runs as a self-healing fallback, and re-running the recalc is safe:
        // recording is idempotent (ExistsForSourceAsync) and Ranking.Refresh folds the whole ledger,
        // so a duplicate recalculation only re-derives the same snapshot. It also self-heals a lost
        // recalc — a failure fails this consume and the redelivery re-runs the recalc even though the
        // already-recorded score entry is deduped. Without this a teammate's target scan only moved
        // the X/n counter; the header score lagged until the substage reveal force-refetched.
        await _sender.Send(
            new RecalculateRankingCommand(
                context.Message.LiveSessionId,
                context.Message.ResolvedAt),
            context.CancellationToken);
    }
}
