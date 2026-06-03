namespace umbral_backend.Application.Sessions.Queries.GetSessionTeamsForParticipant;

public interface IGetSessionTeamsForParticipantExecutor
{
    Task<SessionTeamLobbyDto> GetAsync(
        GetSessionTeamsForParticipantQuery query,
        CancellationToken cancellationToken);
}
