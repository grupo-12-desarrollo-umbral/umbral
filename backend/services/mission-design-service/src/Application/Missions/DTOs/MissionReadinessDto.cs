namespace umbral_backend.Application.Missions.DTOs;

public sealed record MissionReadinessDto(
    int MissionId,
    string ActivationState,
    bool IsReady,
    IReadOnlyList<string>? Failures = null);
