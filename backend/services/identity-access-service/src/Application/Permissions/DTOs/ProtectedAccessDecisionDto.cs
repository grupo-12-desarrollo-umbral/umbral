namespace umbral_backend.Application.Permissions.DTOs;

public sealed record ProtectedAccessDecisionDto(
    string Capability,
    bool IsAllowed,
    string Reason);
