using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

public sealed class BroadcastOperatorSessionPanelNotificationHandler
    : INotificationHandler<SessionStateChangedEvent>,
        INotificationHandler<SubstageAdvancedEvent>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly IOperatorSessionPanelBroadcaster _operatorSessionPanelBroadcaster;
    private readonly TimeProvider _timeProvider;

    public BroadcastOperatorSessionPanelNotificationHandler(
        ILiveSessionRepository liveSessionRepository,
        IOperatorSessionPanelBroadcaster operatorSessionPanelBroadcaster,
        TimeProvider timeProvider)
    {
        _liveSessionRepository = liveSessionRepository;
        _operatorSessionPanelBroadcaster = operatorSessionPanelBroadcaster;
        _timeProvider = timeProvider;
    }

    public Task Handle(SessionStateChangedEvent notification, CancellationToken cancellationToken)
    {
        return BroadcastOperatorSessionPanelAsync(notification.LiveSessionId, cancellationToken);
    }

    public Task Handle(SubstageAdvancedEvent notification, CancellationToken cancellationToken)
    {
        return BroadcastOperatorSessionPanelAsync(notification.LiveSessionId, cancellationToken);
    }

    private async Task BroadcastOperatorSessionPanelAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var liveSession = await _liveSessionRepository.GetByIdAsync(liveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), liveSessionId);

        var snapshot = liveSession.ProjectOperatorSessionPanel(_timeProvider.GetUtcNow());
        var panel = OperatorSessionPanelDtoFactory.Create(liveSession, snapshot);

        await _operatorSessionPanelBroadcaster.BroadcastSessionPanelUpdatedAsync(panel, cancellationToken);
    }
}
