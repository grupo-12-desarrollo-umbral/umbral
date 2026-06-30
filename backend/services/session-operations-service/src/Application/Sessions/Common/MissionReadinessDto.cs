namespace umbral_backend.Application.Sessions.Common;

public sealed record MissionReadinessDto(
    int MissionId,
    string ActivationState,
    bool IsActive,
    bool IsReady,
    IReadOnlyList<string> Failures);
