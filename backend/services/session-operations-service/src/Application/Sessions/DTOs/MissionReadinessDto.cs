namespace umbral_backend.Application.Sessions.DTOs;

public sealed record MissionReadinessDto(
    int MissionId,
    string ActivationState,
    bool IsActive,
    bool IsReady,
    IReadOnlyList<string> Failures);
