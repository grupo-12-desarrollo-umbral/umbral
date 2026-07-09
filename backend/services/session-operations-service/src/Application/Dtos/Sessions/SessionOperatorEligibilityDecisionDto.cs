namespace umbral_backend.Application.Dtos.Sessions;

public sealed record SessionOperatorEligibilityDecisionDto(
    string Source,
    bool IsEligible,
    int OperatorUserId,
    string? Role,
    string? Reason);
