namespace umbral_backend.Application.Common.Interfaces;

public interface ISessionAssignmentReadRepository
{
    Task<Guid?> GetAssignedOperatorUserIdAsync(Guid liveSessionId, CancellationToken cancellationToken);
}
