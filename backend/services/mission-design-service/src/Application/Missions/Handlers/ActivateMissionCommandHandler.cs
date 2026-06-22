using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.ActivateMission;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class ActivateMissionCommandHandler : IRequestHandler<ActivateMissionCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;
    private readonly ITriviaQuizRepository _triviaQuizRepository;

    public ActivateMissionCommandHandler(
        IMissionRepository missionRepository,
        ITriviaQuizRepository triviaQuizRepository)
    {
        _missionRepository = missionRepository;
        _triviaQuizRepository = triviaQuizRepository;
    }

    public async Task<MissionDto> Handle(ActivateMissionCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Mission", request.Id);

        // The domain's Activate() checks structural readiness; the cross-aggregate
        // "still published?" check lives here, where repository access exists.
        await EnsureSelectedQuizzesPublishedAsync(mission, cancellationToken);

        mission.Activate();

        await _missionRepository.UpdateAsync(mission, cancellationToken);

        return MissionDtoMapper.Map(mission);
    }

    private async Task EnsureSelectedQuizzesPublishedAsync(Mission mission, CancellationToken cancellationToken)
    {
        var quizIds = MissionTriviaPublicationChecker.CollectTriviaQuizIds(mission);
        if (quizIds.Count == 0)
        {
            return;
        }

        var statuses = await _triviaQuizRepository.GetStatusesByIdsAsync(quizIds, cancellationToken);
        var failures = MissionTriviaPublicationChecker.Evaluate(mission, statuses);
        if (failures.Count > 0)
        {
            throw new MissionNotReadyForActivationException(failures);
        }
    }
}
