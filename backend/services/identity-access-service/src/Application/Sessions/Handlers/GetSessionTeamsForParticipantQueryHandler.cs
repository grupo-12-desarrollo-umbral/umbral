using umbral_backend.Application.Sessions.Queries.GetSessionTeamsForParticipant;

namespace umbral_backend.Application.Sessions.Handlers;

public sealed class GetSessionTeamsForParticipantQueryHandler
    : IRequestHandler<GetSessionTeamsForParticipantQuery, SessionTeamLobbyDto>
{
    private readonly IGetSessionTeamsForParticipantService _service;

    public GetSessionTeamsForParticipantQueryHandler(IGetSessionTeamsForParticipantService service)
    {
        _service = service;
    }

    public async Task<SessionTeamLobbyDto> Handle(
        GetSessionTeamsForParticipantQuery request,
        CancellationToken cancellationToken)
    {
        return await _service.GetAsync(request, cancellationToken);
    }
}
