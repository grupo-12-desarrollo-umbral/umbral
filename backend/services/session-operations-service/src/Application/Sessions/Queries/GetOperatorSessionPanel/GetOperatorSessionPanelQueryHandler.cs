using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Queries.GetOperatorSessionPanel;

public sealed class GetOperatorSessionPanelQueryHandler
    : IRequestHandler<GetOperatorSessionPanelQuery, OperatorSessionPanelDto>
{
    private readonly ISessionAdministrationAccessResolver _accessResolver;
    private readonly TimeProvider _timeProvider;

    public GetOperatorSessionPanelQueryHandler(
        ISessionAdministrationAccessResolver accessResolver,
        TimeProvider timeProvider)
    {
        _accessResolver = accessResolver;
        _timeProvider = timeProvider;
    }

    public async Task<OperatorSessionPanelDto> Handle(
        GetOperatorSessionPanelQuery request,
        CancellationToken cancellationToken)
    {
        var liveSession = await _accessResolver.GetAuthorizedSessionAsync(
            request.LiveSessionId,
            cancellationToken);

        var snapshot = liveSession.ProjectOperatorSessionPanel(_timeProvider.GetUtcNow());

        return OperatorSessionPanelDtoFactory.Create(liveSession, snapshot);
    }
}
