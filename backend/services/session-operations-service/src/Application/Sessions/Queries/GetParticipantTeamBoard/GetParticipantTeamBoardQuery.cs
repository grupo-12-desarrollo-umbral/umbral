using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Dtos.Sessions;

namespace umbral_backend.Application.Sessions.Queries.GetParticipantTeamBoard;

[Authorize(Roles = "Participant")]
public sealed record GetParticipantTeamBoardQuery(
    Guid LiveSessionId,
    Guid TeamId,
    string? Token) : IRequest<ParticipantTeamBoardDto>;
