namespace umbral_backend.Application.Permissions.Queries.CheckProtectedCapabilityAccess;

public sealed record ProtectedAccessDecisionDto(
    string Capability,
    bool IsAllowed,
    string Reason);
