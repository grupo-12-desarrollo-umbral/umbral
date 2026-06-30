using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Commands.DisconnectParticipant;

public sealed class DisconnectParticipantCommandHandler
    : IRequestHandler<DisconnectParticipantCommand, Unit>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly TimeProvider _timeProvider;

    public DisconnectParticipantCommandHandler(
        ILiveSessionRepository liveSessionRepository,
        TimeProvider timeProvider)
    {
        _liveSessionRepository = liveSessionRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Unit> Handle(
        DisconnectParticipantCommand request,
        CancellationToken cancellationToken)
    {
        var liveSession = await _liveSessionRepository.GetByIdAsync(request.LiveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), request.LiveSessionId);

        liveSession.DisconnectParticipant(request.SessionParticipantId, _timeProvider.GetUtcNow());

        await _liveSessionRepository.UpdateAsync(liveSession, cancellationToken);

        return Unit.Value;
    }
}
