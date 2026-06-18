using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.SetTriviaQuestionSelection;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class SetTriviaQuestionSelectionCommandHandler
    : MissionCommandHandlerBase, IRequestHandler<SetTriviaQuestionSelectionCommand, MissionDto>
{
    private readonly ITriviaQuizRepository _triviaQuizRepository;

    public SetTriviaQuestionSelectionCommandHandler(
        IMissionRepository missionRepository,
        ITriviaQuizRepository triviaQuizRepository)
        : base(missionRepository)
    {
        _triviaQuizRepository = triviaQuizRepository;
    }

    public async Task<MissionDto> Handle(SetTriviaQuestionSelectionCommand request, CancellationToken cancellationToken)
    {
        var mission = await GetMissionAsync(request.MissionId, cancellationToken);
        MissionStructureEditor.FindSubstage(mission, request.StageId, request.SubstageId);

        await TriviaQuestionSelectionGuard.EnsurePublishedSelectionAsync(
            _triviaQuizRepository,
            request.TriviaQuizId,
            cancellationToken);

        mission.SelectTriviaQuiz(request.StageId, request.SubstageId, request.TriviaQuizId);

        await UpdateAndReturnAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }
}
