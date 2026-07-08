using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Queries.GetSessionTeamLobby;

[Authorize(Roles = "Participant")]
public sealed record GetSessionTeamLobbyByCodeQuery(string SessionCode)
    : IRequest<SessionTeamLobbyDto>;
