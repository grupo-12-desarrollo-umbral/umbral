namespace umbral_backend.Application.Sessions.Common;

public sealed record SessionOperatorEligibilityDecisionDto(
    string Source,
    bool IsEligible,
    int OperatorUserId,
    string? Role,
    string? Reason);
