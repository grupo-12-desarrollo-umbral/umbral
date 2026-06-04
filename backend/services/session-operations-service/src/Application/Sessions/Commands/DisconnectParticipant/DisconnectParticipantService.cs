using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Commands.DisconnectParticipant;

public sealed class DisconnectParticipantService : IDisconnectParticipantExecutor
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly TimeProvider _timeProvider;

    public DisconnectParticipantService(
        ILiveSessionRepository liveSessionRepository,
        TimeProvider timeProvider)
    {
        _liveSessionRepository = liveSessionRepository;
        _timeProvider = timeProvider;
    }

    public async Task DisconnectAsync(
        DisconnectParticipantCommand command,
        CancellationToken cancellationToken)
    {
        var liveSession = await _liveSessionRepository.GetByIdAsync(command.LiveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), command.LiveSessionId);

        liveSession.DisconnectParticipant(command.SessionParticipantId, _timeProvider.GetUtcNow());

        await _liveSessionRepository.UpdateAsync(liveSession, cancellationToken);
    }
}
