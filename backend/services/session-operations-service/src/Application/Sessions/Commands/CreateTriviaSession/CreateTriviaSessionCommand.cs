using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Sessions.Commands.CreateTriviaSession;

[Authorize(Roles = "Operator")]
public sealed record CreateTriviaSessionCommand(
    int SourceTriviaQuizId,
    string Title,
    int MaximumTimeMinutes,
    DateTimeOffset ScheduledAt) : IRequest<CreateTriviaSessionResultDto>;
