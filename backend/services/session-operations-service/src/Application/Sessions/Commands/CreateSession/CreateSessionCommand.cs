using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Sessions.Commands.CreateSession;

[Authorize(Roles = "Administrator")]
public sealed record CreateSessionCommand(
    int MissionId,
    string Title,
    int MaximumTimeMinutes,
    DateTimeOffset ScheduledAt) : IRequest<CreateSessionResultDto>;
