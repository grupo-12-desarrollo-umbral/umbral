namespace umbral_backend.Application.Dtos.Sessions;

public sealed record MissionReadinessDto(
    int MissionId,
    string ActivationState,
    bool IsActive,
    bool IsReady,
    IReadOnlyList<string> Failures);
