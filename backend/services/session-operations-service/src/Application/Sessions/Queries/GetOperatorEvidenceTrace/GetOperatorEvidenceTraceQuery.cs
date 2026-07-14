using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Sessions.Queries.GetOperatorEvidenceTrace;

// HU-32: operator evidence traceability read surface. Returns the full trace list for a live session,
// optionally filtered by team, gated to the assigned operator by the ownership resolver Proxy.
[Authorize(Roles = "Operator")]
public sealed record GetOperatorEvidenceTraceQuery(Guid LiveSessionId, Guid? TeamId = null)
    : IRequest<EvidenceTraceDto>;
