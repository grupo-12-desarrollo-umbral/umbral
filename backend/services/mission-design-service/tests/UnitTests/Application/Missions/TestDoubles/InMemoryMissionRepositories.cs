using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.Queries.GetDifficultyCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionReadiness;
using umbral_backend.Application.Missions.Queries.GetMissionRuntimePlan;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Application.Missions.TestDoubles;

internal sealed class InMemoryMissionRepository : IMissionRepository
{
    private readonly Dictionary<int, Mission> _missions = new();
    private int _nextId = 1;

    public Mission? LastAddedMission { get; private set; }

    public Mission? LastUpdatedMission { get; private set; }

    public Task<Mission?> GetByIdAsync(int missionId, CancellationToken cancellationToken)
    {
        _missions.TryGetValue(missionId, out var mission);
        return Task.FromResult(mission);
    }

    public Task<IReadOnlyList<ActiveMissionReference>> GetActiveMissionsReferencingTriviaQuizAsync(
        int triviaQuizId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ActiveMissionReference> references = _missions.Values
            .Where(mission => mission.ActivationState == MissionActivation.Ready)
            .Where(mission => mission.Stages
                .SelectMany(stage => stage.Substages)
                .Any(substage => substage.TriviaQuizId == triviaQuizId))
            .Select(mission => new ActiveMissionReference(mission.Id, mission.Name))
            .ToList();

        return Task.FromResult(references);
    }

    public Task AddAsync(Mission mission, CancellationToken cancellationToken)
    {
        LastAddedMission = mission;

        if (mission.Id == default)
        {
            mission.Id = _nextId++;
        }

        _missions[mission.Id] = mission;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Mission mission, CancellationToken cancellationToken)
    {
        LastUpdatedMission = mission;
        _missions[mission.Id] = mission;
        return Task.CompletedTask;
    }

    public void Seed(Mission mission)
    {
        if (mission.Id == default)
        {
            mission.Id = _nextId++;
        }

        _missions[mission.Id] = mission;
    }
}

/// <summary>
/// Reports a fixed set of active missions referencing any quiz, so ADR-0003 guard rejection can be
/// exercised without building a Ready mission tree. Records the quiz id it was asked about.
/// </summary>
internal sealed class StubActiveMissionReferenceRepository : IMissionRepository
{
    private readonly IReadOnlyList<ActiveMissionReference> _references;

    public StubActiveMissionReferenceRepository(params ActiveMissionReference[] references)
    {
        _references = references;
    }

    public int? QueriedTriviaQuizId { get; private set; }

    public Task<IReadOnlyList<ActiveMissionReference>> GetActiveMissionsReferencingTriviaQuizAsync(
        int triviaQuizId,
        CancellationToken cancellationToken)
    {
        QueriedTriviaQuizId = triviaQuizId;
        return Task.FromResult(_references);
    }

    public Task<Mission?> GetByIdAsync(int missionId, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task AddAsync(Mission mission, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task UpdateAsync(Mission mission, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}

internal sealed class InMemoryMissionReadModelRepository : IMissionReadModelRepository
{
    private readonly IReadOnlyList<MissionSummaryDto> _catalog;
    private readonly Dictionary<int, MissionDto> _details;
    private readonly Dictionary<int, MissionRuntimePlanDto> _runtimePlans;

    public InMemoryMissionReadModelRepository(
        IReadOnlyList<MissionSummaryDto>? catalog = null,
        IReadOnlyDictionary<int, MissionDto>? details = null,
        IReadOnlyDictionary<int, MissionRuntimePlanDto>? runtimePlans = null)
    {
        _catalog = catalog ?? Array.Empty<MissionSummaryDto>();
        _details = details?.ToDictionary(pair => pair.Key, pair => pair.Value)
            ?? new Dictionary<int, MissionDto>();
        _runtimePlans = runtimePlans?.ToDictionary(pair => pair.Key, pair => pair.Value)
            ?? new Dictionary<int, MissionRuntimePlanDto>();
    }

    public Task<IReadOnlyList<MissionSummaryDto>> GetMissionCatalogAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_catalog);
    }

    public Task<MissionDto?> GetMissionDetailAsync(int missionId, CancellationToken cancellationToken)
    {
        _details.TryGetValue(missionId, out var mission);
        return Task.FromResult(mission);
    }

    public Task<MissionRuntimePlanDto?> GetMissionRuntimePlanAsync(int missionId, CancellationToken cancellationToken)
    {
        _runtimePlans.TryGetValue(missionId, out var missionRuntimePlan);
        return Task.FromResult(missionRuntimePlan);
    }
}

internal sealed class StubClock : IClock
{
    public StubClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; }
}
