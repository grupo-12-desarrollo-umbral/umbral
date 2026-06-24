using umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;

public sealed class ReconnectAuthenticatedParticipantCommandHandler
    : IRequestHandler<ReconnectAuthenticatedParticipantCommand, ReconnectParticipantResultDto>
{
    private readonly IReconnectAuthenticatedParticipantService _reconnectService;

    public ReconnectAuthenticatedParticipantCommandHandler(IReconnectAuthenticatedParticipantService reconnectService)
    {
        _reconnectService = reconnectService;
    }

    public Task<ReconnectParticipantResultDto> Handle(
        ReconnectAuthenticatedParticipantCommand request,
        CancellationToken cancellationToken)
    {
        return _reconnectService.ReconnectAsync(request, cancellationToken);
    }
}
