using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Application.Missions.Queries.GetMissionReadiness;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class GetMissionReadinessQueryHandler : IRequestHandler<GetMissionReadinessQuery, MissionReadinessDto>
{
    private readonly IMissionRepository _missionRepository;
    private readonly ITriviaQuizRepository _triviaQuizRepository;

    public GetMissionReadinessQueryHandler(
        IMissionRepository missionRepository,
        ITriviaQuizRepository triviaQuizRepository)
    {
        _missionRepository = missionRepository;
        _triviaQuizRepository = triviaQuizRepository;
    }

    public async Task<MissionReadinessDto> Handle(GetMissionReadinessQuery request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Mission", request.Id);

        // Structural readiness from the pure domain policy, then a live re-check that every
        // selected trivia quiz is still published (a quiz can be archived post-selection).
        var failures = MissionActivationPolicy.EvaluateReadiness(mission).ToList();

        var quizIds = MissionTriviaPublicationChecker.CollectTriviaQuizIds(mission);
        if (quizIds.Count > 0)
        {
            var statuses = await _triviaQuizRepository.GetStatusesByIdsAsync(quizIds, cancellationToken);
            failures.AddRange(MissionTriviaPublicationChecker.Evaluate(mission, statuses));
        }

        return new MissionReadinessDto(
            mission.Id,
            mission.ActivationState.ToString(),
            failures.Count == 0,
            failures);
    }
}
