namespace umbral_backend.Application.Teams.Commands.JoinTeamAsParticipant;

public interface IJoinTeamAsParticipantExecutor
{
    Task<Guid> JoinAsync(JoinTeamAsParticipantCommand command, CancellationToken cancellationToken);
}
