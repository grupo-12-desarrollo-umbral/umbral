using umbral_backend.Application.Sessions.Commands.DisconnectParticipant;

namespace umbral_backend.Application.Sessions.Handlers;

public sealed class DisconnectParticipantCommandHandler
    : IRequestHandler<DisconnectParticipantCommand, Unit>
{
    private readonly IDisconnectParticipantService _disconnectService;

    public DisconnectParticipantCommandHandler(IDisconnectParticipantService disconnectService)
    {
        _disconnectService = disconnectService;
    }

    public async Task<Unit> Handle(
        DisconnectParticipantCommand request,
        CancellationToken cancellationToken)
    {
        await _disconnectService.DisconnectAsync(request, cancellationToken);
        return Unit.Value;
    }
}
