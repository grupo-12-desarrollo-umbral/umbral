using umbral_backend.Domain.Entities;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Common.Interfaces;

public interface ILiveSessionRepository
{
    Task<LiveSession?> GetByIdAsync(Guid liveSessionId, CancellationToken cancellationToken);

    Task<LiveSession?> GetBySessionCodeAsync(string sessionCode, CancellationToken cancellationToken);

    Task<LiveSession?> GetTimerSessionByIdAsync(Guid liveSessionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionOperatorSummaryDto>> ListAssignableSummariesAsync(
        int? assignedOperatorUserId,
        bool includeConcluded,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LiveSession>> ListActiveTimersAsync(CancellationToken cancellationToken);

    Task UpdateAsync(LiveSession liveSession, CancellationToken cancellationToken);
}
