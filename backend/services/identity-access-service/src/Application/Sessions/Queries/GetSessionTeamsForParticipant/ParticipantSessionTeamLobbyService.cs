using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Queries.GetSessionTeamsForParticipant;

public sealed class ParticipantSessionTeamLobbyService : IGetSessionTeamsForParticipantService
{
    private readonly ILiveSessionReferenceRepository _liveSessionReferenceRepository;

    public ParticipantSessionTeamLobbyService(ILiveSessionReferenceRepository liveSessionReferenceRepository)
    {
        _liveSessionReferenceRepository = liveSessionReferenceRepository;
    }

    public async Task<SessionTeamLobbyDto> GetAsync(
        GetSessionTeamsForParticipantQuery query,
        CancellationToken cancellationToken)
    {
        var normalizedCode = query.SessionCode.Trim().ToUpperInvariant();
        var liveSessionReference = await _liveSessionReferenceRepository.GetBySessionCodeAsync(
            normalizedCode,
            cancellationToken);

        if (liveSessionReference is null)
        {
            throw new NotFoundException(nameof(LiveSessionReference), normalizedCode);
        }

        var entries = await _liveSessionReferenceRepository.ListTeamLobbyEntriesAsync(
            liveSessionReference.LiveSessionId,
            cancellationToken);

        var callerHasMembershipInSession = entries.Any(entry => entry.IsCallerMember);
        var teams = entries
            .Select(entry => new SessionTeamLobbyTeamDto(
                entry.TeamId,
                entry.DisplayName,
                entry.IsCallerMember
                    ? "mine"
                    : callerHasMembershipInSession
                        ? "locked"
                        : "joinable"))
            .ToArray();

        return new SessionTeamLobbyDto(
            liveSessionReference.LiveSessionId,
            liveSessionReference.SessionCode,
            teams);
    }
}
