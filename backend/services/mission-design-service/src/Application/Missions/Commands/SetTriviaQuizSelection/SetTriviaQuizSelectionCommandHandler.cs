using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.SetTriviaQuizSelection;
using umbral_backend.Application.Missions.Common;

namespace umbral_backend.Application.Missions.Commands.SetTriviaQuizSelection;

public sealed class SetTriviaQuizSelectionCommandHandler
    : IRequestHandler<SetTriviaQuizSelectionCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;
    private readonly ITriviaQuizRepository _triviaQuizRepository;

    public SetTriviaQuizSelectionCommandHandler(
        IMissionRepository missionRepository,
        ITriviaQuizRepository triviaQuizRepository)
    {
        _missionRepository = missionRepository;
        _triviaQuizRepository = triviaQuizRepository;
    }

    public async Task<MissionDto> Handle(SetTriviaQuizSelectionCommand request, CancellationToken cancellationToken)
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
