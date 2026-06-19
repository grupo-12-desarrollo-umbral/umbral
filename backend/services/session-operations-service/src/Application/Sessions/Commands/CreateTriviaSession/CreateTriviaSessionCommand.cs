using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Sessions.Commands.CreateTriviaSession;

[Authorize(Roles = "Administrator")]
public sealed record CreateTriviaSessionCommand(
    int MissionId,
    int SourceTriviaQuizId,
    string Title,
    int MaximumTimeMinutes,
    DateTimeOffset ScheduledAt) : IRequest<CreateTriviaSessionResultDto>;
