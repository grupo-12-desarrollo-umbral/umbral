namespace umbral_backend.Application.Common.Interfaces;

public interface ISessionAssignmentProjectionRepository
{
    Task UpsertAsync(Guid liveSessionId, Guid assignedOperatorUserId, CancellationToken cancellationToken);
}
