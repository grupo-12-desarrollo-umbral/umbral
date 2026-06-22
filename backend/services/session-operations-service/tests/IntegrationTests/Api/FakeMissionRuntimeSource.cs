using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

public sealed class FakeMissionRuntimeSource : IMissionRuntimeSource
{
    public MissionRuntimeDto? Runtime { get; set; }

    public Task<MissionRuntimeDto?> GetByIdAsync(int missionId, CancellationToken cancellationToken)
    {
        return Task.FromResult(Runtime);
    }
}
