namespace umbral_backend.Application.Sessions.DTOs;

public sealed record SessionOperatorEligibilityDecisionDto(
    string Source,
    bool IsEligible,
    int OperatorUserId,
    string? Role,
    string? Reason);
