using MassTransit;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.SessionEvents.Common;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.SessionEvents.Consumers;

public sealed class SessionEventHistoryConsumer :
    IConsumer<SessionStateChangedIntegrationEvent>,
    IConsumer<QuestionClosedIntegrationEvent>,
    IConsumer<SessionResultsFinalizedIntegrationEvent>
{
    private readonly ISessionEventHistoryRepository _historyRepository;

    public SessionEventHistoryConsumer(ISessionEventHistoryRepository historyRepository)
    {
        _historyRepository = historyRepository;
    }

    public Task Consume(ConsumeContext<SessionStateChangedIntegrationEvent> context)
    {
        var message = context.Message;
        return _historyRepository.AppendAsync(
            SessionEvent.ForStateChange(
                message.LiveSessionId,
                message.PreviousState,
                message.CurrentState,
                message.ChangedAt,
                message.ResponsibleUserExternalId,
                message.Reason),
            context.CancellationToken);
    }

    public Task Consume(ConsumeContext<QuestionClosedIntegrationEvent> context)
    {
        var message = context.Message;
        return _historyRepository.AppendAsync(
            SessionEvent.ForQuestionClosed(message.LiveSessionId, message.QuestionIndex, message.ClosedAt),
            context.CancellationToken);
    }

    public Task Consume(ConsumeContext<SessionResultsFinalizedIntegrationEvent> context)
    {
        var message = context.Message;
        return _historyRepository.AppendAsync(
            SessionEvent.ForResultsFinalized(message.LiveSessionId, message.FinishedAt),
            context.CancellationToken);
    }
}
