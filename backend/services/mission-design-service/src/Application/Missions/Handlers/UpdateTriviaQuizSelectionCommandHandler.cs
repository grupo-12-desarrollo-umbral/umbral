using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.UpdateTriviaQuizSelection;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class UpdateTriviaQuizSelectionCommandHandler
    : MissionCommandHandlerBase, IRequestHandler<UpdateTriviaQuizSelectionCommand, MissionDto>
{
    private readonly ITriviaQuizRepository _triviaQuizRepository;

    public UpdateTriviaQuizSelectionCommandHandler(
        IMissionRepository missionRepository,
        ITriviaQuizRepository triviaQuizRepository)
        : base(missionRepository)
    {
        _triviaQuizRepository = triviaQuizRepository;
    }

    public async Task<MissionDto> Handle(UpdateTriviaQuizSelectionCommand request, CancellationToken cancellationToken)
    {
        var mission = await GetMissionAsync(request.MissionId, cancellationToken);
        MissionStructureEditor.FindSubstage(mission, request.StageId, request.SubstageId);

        await TriviaQuizSelectionGuard.EnsurePublishedSelectionAsync(
            _triviaQuizRepository,
            request.TriviaQuizId,
            cancellationToken);

        mission.SelectTriviaQuiz(request.StageId, request.SubstageId, request.TriviaQuizId);

        await UpdateAndReturnAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }
}
