using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Queries.GetOperatorEvidenceTrace;

public sealed class GetOperatorEvidenceTraceQueryHandler
    : IRequestHandler<GetOperatorEvidenceTraceQuery, EvidenceTraceDto>
{
    private readonly ISessionAdministrationAccessResolver _accessResolver;
    private readonly IEvidenceTraceRepository _traceRepository;

    public GetOperatorEvidenceTraceQueryHandler(
        ISessionAdministrationAccessResolver accessResolver,
        IEvidenceTraceRepository traceRepository)
    {
        _accessResolver = accessResolver;
        _traceRepository = traceRepository;
    }

    public async Task<EvidenceTraceDto> Handle(
        GetOperatorEvidenceTraceQuery request,
        CancellationToken cancellationToken)
    {
        // Ownership is delegated to the resolver Proxy (ADR-0009): Administrator sees all, Operator only
        // their assigned session, else ForbiddenAccessException. No ad-hoc role/owner check lives here.
        var liveSession = await _accessResolver.GetAuthorizedSessionAsync(
            request.LiveSessionId, cancellationToken);

        var entries = await _traceRepository.ListBySessionAsync(
            liveSession.LiveSessionId, request.TeamId, cancellationToken);

        return EvidenceTraceDtoFactory.Create(liveSession.LiveSessionId, entries);
    }
}
