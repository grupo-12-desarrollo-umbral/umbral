namespace umbral_backend.Application.Dtos.Missions;

public sealed record MissionReadinessDto(
    int MissionId,
    string ActivationState,
    bool IsReady,
    IReadOnlyList<string>? Failures = null);
