using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Missions.Commands.SetTriviaQuestionSelection;

[Authorize(Roles = Roles.Administrator)]
public sealed record SetTriviaQuestionSelectionCommand(
    int MissionId,
    int StageId,
    int SubstageId,
    int TriviaQuizId) : IRequest<MissionDto>;
