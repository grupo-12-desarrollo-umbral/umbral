using umbral_backend.Application.Dtos.Permissions;

namespace umbral_backend.Application.Permissions.Queries.GetParticipantEligibleTeams;

public sealed record GetParticipantEligibleTeamsQuery : IRequest<ParticipantEligibleTeamsDto>;
