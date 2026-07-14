using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Queries.GetReleasableClues;

public sealed class GetReleasableCluesQueryHandler
    : IRequestHandler<GetReleasableCluesQuery, ReleasableCluesDto>
{
    private readonly ISessionAdministrationAccessResolver _accessResolver;

    public GetReleasableCluesQueryHandler(ISessionAdministrationAccessResolver accessResolver)
    {
        _accessResolver = accessResolver;
    }

    public async Task<ReleasableCluesDto> Handle(
        GetReleasableCluesQuery request,
        CancellationToken cancellationToken)
    {
        var liveSession = await _accessResolver.GetAuthorizedSessionAsync(
            request.LiveSessionId,
            cancellationToken);

        return ReleasableCluesDtoFactory.Create(liveSession);
    }
}
