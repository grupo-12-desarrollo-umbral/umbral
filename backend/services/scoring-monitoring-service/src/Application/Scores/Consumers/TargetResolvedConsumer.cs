using MassTransit;
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
                context.Message.EvidenceSubmissionId),
            context.CancellationToken);
    }
}
