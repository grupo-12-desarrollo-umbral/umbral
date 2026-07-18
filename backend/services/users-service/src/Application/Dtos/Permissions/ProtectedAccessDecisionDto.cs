namespace umbral_backend.Application.Dtos.Permissions;

public sealed record ProtectedAccessDecisionDto(
    string Capability,
    bool IsAllowed,
    string Reason);
