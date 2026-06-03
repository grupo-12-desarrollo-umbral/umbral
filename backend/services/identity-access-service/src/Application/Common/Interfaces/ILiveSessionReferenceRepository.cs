using umbral_backend.Application.Sessions.Queries.GetSessionTeamsForParticipant;
using umbral_backend.Domain.Entities;
using umbral_backend.Application.Common.Models;

namespace umbral_backend.Application.Common.Interfaces;

public interface ILiveSessionReferenceRepository
{
    Task<LiveSessionReference?> GetByIdAsync(Guid liveSessionId, CancellationToken cancellationToken);

    Task<LiveSessionReference?> GetBySessionCodeAsync(string sessionCode, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionTeamLobbyEntry>> ListTeamLobbyEntriesAsync(
        Guid liveSessionId,
        CancellationToken cancellationToken);

    Task<bool> IsTeamAssociatedAsync(Guid liveSessionId, Guid teamId, CancellationToken cancellationToken);

    Task<ParticipantSessionMembershipLookup?> GetParticipantMembershipAsync(
        Guid liveSessionId,
        int userId,
        CancellationToken cancellationToken);

    Task AddAsync(LiveSessionReference liveSessionReference, CancellationToken cancellationToken);
}
