using MassTransit;
using umbral_backend.Application.Rankings.Commands.RecalculateRanking;
using umbral_backend.Application.Scores.Common;

namespace umbral_backend.Application.Rankings.Consumers;

public sealed class ScoreEntryRegisteredConsumer : IConsumer<ScoreEntryRegisteredIntegrationEvent>
{
    private readonly ISender _sender;

    public ScoreEntryRegisteredConsumer(ISender sender)
    {
        _sender = sender;
    }

    public async Task Consume(ConsumeContext<ScoreEntryRegisteredIntegrationEvent> context)
    {
        await _sender.Send(
            new RecalculateRankingCommand(
                context.Message.LiveSessionId,
                context.Message.RecordedAt),
            context.CancellationToken);
    }
}
