using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.ReleaseClue;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Sessions.Common;

public sealed class ClueReleaseFacade : IClueReleaseFacade
{
    private readonly ISessionAdministrationAccessResolver _sessionAdministrationAccessResolver;
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly TimeProvider _timeProvider;

    public ClueReleaseFacade(
        ISessionAdministrationAccessResolver sessionAdministrationAccessResolver,
        ILiveSessionRepository liveSessionRepository,
        TimeProvider timeProvider)
    {
        _sessionAdministrationAccessResolver = sessionAdministrationAccessResolver;
        _liveSessionRepository = liveSessionRepository;
        _timeProvider = timeProvider;
    }

    public async Task<ReleaseClueResultDto> ReleaseCluesAsync(
        ReleaseClueCommand command,
        CancellationToken cancellationToken)
    {
        var liveSession = await _sessionAdministrationAccessResolver.GetAuthorizedSessionAsync(
            command.LiveSessionId,
            cancellationToken);
        var operatorUserId = liveSession.AssignedOperatorUserId!.Value;
        var subject = ClueReleaseSubject.Create(command.TargetId, command.ClueId);
        var releasedAt = _timeProvider.GetUtcNow();

        IReadOnlyCollection<Guid> releasedTeamIds;
        if (command.TeamId.HasValue)
        {
            liveSession.ReleaseClueToTeam(
                subject,
                command.TeamId.Value,
                operatorUserId,
                releasedAt);
            releasedTeamIds = [command.TeamId.Value];
        }
        else
        {
            liveSession.ReleaseClueToAllTeams(
                subject,
                operatorUserId,
                releasedAt);
            releasedTeamIds = liveSession.Teams.Select(team => team.TeamId).ToArray();
        }

        await _liveSessionRepository.UpdateAsync(liveSession, cancellationToken);

        return new ReleaseClueResultDto(command.TargetId, command.ClueId, releasedTeamIds);
    }
}
