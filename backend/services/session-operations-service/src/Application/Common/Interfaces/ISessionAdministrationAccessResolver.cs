using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Common.Interfaces;

public interface ISessionAdministrationAccessResolver
{
    Task<LiveSession> GetAuthorizedSessionAsync(Guid liveSessionId, CancellationToken cancellationToken);

    Task<(LiveSession Session, int? ResponsibleUserId)> GetAuthorizedSessionWithActorAsync(
        Guid liveSessionId,
        CancellationToken cancellationToken);

    Task<LiveSession> GetAuthorizedTimerSessionAsync(Guid liveSessionId, CancellationToken cancellationToken);
}
