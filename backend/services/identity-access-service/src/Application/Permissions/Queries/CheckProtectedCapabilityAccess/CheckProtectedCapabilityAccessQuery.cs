using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Permissions.Queries.CheckProtectedCapabilityAccess;

public sealed record CheckProtectedCapabilityAccessQuery(
    ProtectedCapability Capability) : IRequest<ProtectedAccessDecisionDto>;
