using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Common.Interfaces;

public interface IJoinTokenRepository
{
    Task<JoinToken?> GetByIdAsync(Guid joinTokenId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    Task<JoinToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    Task<JoinToken?> GetByLiveSessionIdAndTeamIdAsync(
        Guid liveSessionId,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    Task AddAsync(JoinToken joinToken, CancellationToken cancellationToken);
}
