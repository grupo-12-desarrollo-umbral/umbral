namespace umbral_backend.Application.Sessions.Queries.GetSessionTeamsForParticipant;

public interface IGetSessionTeamsForParticipantService
{
    Task<SessionTeamLobbyDto> GetAsync(
        GetSessionTeamsForParticipantQuery query,
        CancellationToken cancellationToken);
}
