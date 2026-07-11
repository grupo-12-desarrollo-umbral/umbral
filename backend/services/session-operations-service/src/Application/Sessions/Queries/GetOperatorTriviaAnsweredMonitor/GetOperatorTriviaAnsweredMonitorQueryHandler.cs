using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Queries.GetOperatorTriviaAnsweredMonitor;

public sealed class GetOperatorTriviaAnsweredMonitorQueryHandler
    : IRequestHandler<GetOperatorTriviaAnsweredMonitorQuery, TriviaAnsweredMonitorDto>
{
    private readonly ISessionAdministrationAccessResolver _accessResolver;

    public GetOperatorTriviaAnsweredMonitorQueryHandler(ISessionAdministrationAccessResolver accessResolver)
    {
        _accessResolver = accessResolver;
    }

    public async Task<TriviaAnsweredMonitorDto> Handle(
        GetOperatorTriviaAnsweredMonitorQuery request,
        CancellationToken cancellationToken)
    {
        // Ownership is delegated to the resolver Proxy (ADR-0009): Administrator sees all, Operator only
        // their assigned session, else ForbiddenAccessException. No ad-hoc role/owner check lives here.
        var liveSession = await _accessResolver.GetAuthorizedSessionAsync(
            request.LiveSessionId, cancellationToken);

        var snapshot = liveSession.ProjectActiveQuestionAnsweredStatus();

        return TriviaAnsweredMonitorDtoFactory.Create(liveSession, snapshot);
    }
}
