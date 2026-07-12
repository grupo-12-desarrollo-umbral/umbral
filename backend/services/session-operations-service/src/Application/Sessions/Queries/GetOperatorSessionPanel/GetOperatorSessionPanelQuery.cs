using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Sessions.Queries.GetOperatorSessionPanel;

[Authorize(Roles = "Operator")]
public sealed record GetOperatorSessionPanelQuery(Guid LiveSessionId)
    : IRequest<OperatorSessionPanelDto>;
