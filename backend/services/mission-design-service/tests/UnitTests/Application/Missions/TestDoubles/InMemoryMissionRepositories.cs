using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Domain.Entities;

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

internal sealed class InMemoryMissionReadModelRepository : IMissionReadModelRepository
{
    private readonly IReadOnlyList<MissionSummaryDto> _catalog;
    private readonly Dictionary<int, MissionDto> _details;

    public InMemoryMissionReadModelRepository(
        IReadOnlyList<MissionSummaryDto>? catalog = null,
        IReadOnlyDictionary<int, MissionDto>? details = null)
    {
        _catalog = catalog ?? Array.Empty<MissionSummaryDto>();
        _details = details?.ToDictionary(pair => pair.Key, pair => pair.Value)
            ?? new Dictionary<int, MissionDto>();
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
}

internal sealed class StubClock : IClock
{
    public StubClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; }
}
