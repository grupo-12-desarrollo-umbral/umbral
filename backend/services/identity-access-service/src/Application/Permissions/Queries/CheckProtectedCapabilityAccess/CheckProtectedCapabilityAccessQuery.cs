using umbral_backend.Application.Dtos.Permissions;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Permissions.Queries.CheckProtectedCapabilityAccess;

public sealed record CheckProtectedCapabilityAccessQuery(
    ProtectedCapability Capability) : IRequest<ProtectedAccessDecisionDto>;
