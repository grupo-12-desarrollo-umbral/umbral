using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

public sealed class FakeMissionReadinessSource : IMissionReadinessSource
{
    public MissionReadinessDto? Readiness { get; set; }

    public Task<MissionReadinessDto?> GetByIdAsync(int missionId, CancellationToken cancellationToken)
    {
        return Task.FromResult(
            Readiness is not null && Readiness.MissionId == missionId
                ? Readiness
                : null);
    }
}
