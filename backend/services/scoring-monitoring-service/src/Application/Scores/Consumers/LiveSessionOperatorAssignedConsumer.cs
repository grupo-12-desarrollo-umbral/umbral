using MassTransit;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Scores.Common;

namespace umbral_backend.Application.Scores.Consumers;

public sealed class LiveSessionOperatorAssignedConsumer : IConsumer<LiveSessionOperatorAssignedIntegrationEvent>
{
    private readonly ISessionAssignmentProjectionRepository _projectionRepository;

    public LiveSessionOperatorAssignedConsumer(ISessionAssignmentProjectionRepository projectionRepository)
    {
        _projectionRepository = projectionRepository;
    }

    public async Task Consume(ConsumeContext<LiveSessionOperatorAssignedIntegrationEvent> context)
    {
        await _projectionRepository.UpsertAsync(
            context.Message.LiveSessionId,
            context.Message.AssignedOperatorUserId,
            context.CancellationToken);
    }
}
