using umbral_backend.Domain.Entities;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Common.Interfaces;

public interface ILiveSessionRepository
{
    Task<LiveSession?> GetByIdAsync(Guid liveSessionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionOperatorSummaryDto>> ListAssignableSummariesAsync(CancellationToken cancellationToken);

    Task UpdateAsync(LiveSession liveSession, CancellationToken cancellationToken);
}
