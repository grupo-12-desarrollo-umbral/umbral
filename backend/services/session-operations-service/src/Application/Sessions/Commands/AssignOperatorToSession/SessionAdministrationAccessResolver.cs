using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Commands.AssignOperatorToSession;

public sealed class SessionAdministrationAccessResolver : ISessionAdministrationAccessExecutor
{
    private readonly ILiveSessionRepository _liveSessionRepository;

    public SessionAdministrationAccessResolver(ILiveSessionRepository liveSessionRepository)
    {
        _liveSessionRepository = liveSessionRepository;
    }

    public async Task<LiveSession> GetAuthorizedSessionAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        return await _liveSessionRepository.GetByIdAsync(liveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), liveSessionId);
    }
}
