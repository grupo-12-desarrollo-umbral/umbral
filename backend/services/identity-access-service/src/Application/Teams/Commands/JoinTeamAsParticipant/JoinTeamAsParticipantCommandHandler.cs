using umbral_backend.Application.Teams.Commands.JoinTeamAsParticipant;

namespace umbral_backend.Application.Teams.Commands.JoinTeamAsParticipant;

public sealed class JoinTeamAsParticipantCommandHandler : IRequestHandler<JoinTeamAsParticipantCommand, Guid>
{
    private readonly IJoinTeamAsParticipantService _service;

    public JoinTeamAsParticipantCommandHandler(IJoinTeamAsParticipantService service)
    {
        _service = service;
    }

    public async Task<Guid> Handle(JoinTeamAsParticipantCommand request, CancellationToken cancellationToken)
    {
        return await _service.JoinAsync(request, cancellationToken);
    }
}
