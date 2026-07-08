using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Commands.SelectTeam;

// Participant self-join (#110): pick/switch a runtime team before the session starts (Open Team Selection).
[Authorize(Roles = "Participant")]
public sealed record SelectTeamCommand(
    string SessionCode,
    Guid RuntimeTeamId) : IRequest<SelectTeamResultDto>;
