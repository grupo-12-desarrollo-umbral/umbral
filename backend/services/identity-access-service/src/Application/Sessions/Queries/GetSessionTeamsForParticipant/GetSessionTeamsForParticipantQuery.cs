using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Sessions.Queries.GetSessionTeamsForParticipant;

[Authorize(Roles = "Participant")]
public sealed record GetSessionTeamsForParticipantQuery(string SessionCode)
    : IRequest<SessionTeamLobbyDto>;
