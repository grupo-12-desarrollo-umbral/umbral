namespace umbral_backend.Application.Teams.Commands.JoinTeamAsParticipant;

public interface IJoinTeamAsParticipantService
{
    Task<Guid> JoinAsync(JoinTeamAsParticipantCommand command, CancellationToken cancellationToken);
}
