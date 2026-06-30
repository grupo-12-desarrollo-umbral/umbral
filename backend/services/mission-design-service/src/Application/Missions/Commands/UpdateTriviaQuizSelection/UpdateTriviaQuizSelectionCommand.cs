using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Missions.Commands.UpdateTriviaQuizSelection;

[Authorize(Roles = Roles.Administrator)]
public sealed record UpdateTriviaQuizSelectionCommand(
    int MissionId,
    int StageId,
    int SubstageId,
    int TriviaQuizId) : IRequest<MissionDto>;
