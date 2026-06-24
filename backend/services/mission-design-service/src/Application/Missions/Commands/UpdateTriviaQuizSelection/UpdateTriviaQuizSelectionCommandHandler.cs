using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.UpdateTriviaQuizSelection;
using umbral_backend.Application.Missions.Common;

namespace umbral_backend.Application.Missions.Commands.UpdateTriviaQuizSelection;

public sealed class UpdateTriviaQuizSelectionCommandHandler
    : IRequestHandler<UpdateTriviaQuizSelectionCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;
    private readonly ITriviaQuizRepository _triviaQuizRepository;

    public UpdateTriviaQuizSelectionCommandHandler(
        IMissionRepository missionRepository,
        ITriviaQuizRepository triviaQuizRepository)
    {
        _missionRepository = missionRepository;
        _triviaQuizRepository = triviaQuizRepository;
    }

    public async Task<MissionDto> Handle(UpdateTriviaQuizSelectionCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException("Mission", request.MissionId);
        MissionStructureEditor.FindSubstage(mission, request.StageId, request.SubstageId);

        await TriviaQuizSelectionGuard.EnsurePublishedSelectionAsync(
            _triviaQuizRepository,
            request.TriviaQuizId,
            cancellationToken);

        mission.SelectTriviaQuiz(request.StageId, request.SubstageId, request.TriviaQuizId);

        await _missionRepository.UpdateAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }
}
