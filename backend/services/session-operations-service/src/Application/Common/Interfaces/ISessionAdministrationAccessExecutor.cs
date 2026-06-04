using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Common.Interfaces;

public interface ISessionAdministrationAccessExecutor
{
    Task<LiveSession> GetAuthorizedSessionAsync(Guid liveSessionId, CancellationToken cancellationToken);
}
