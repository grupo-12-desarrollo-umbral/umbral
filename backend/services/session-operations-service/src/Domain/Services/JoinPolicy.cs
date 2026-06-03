using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Services;

public sealed class JoinPolicy
{
    public void EnsureCanJoin(LiveSession session, Team team, int teamCapacity)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(team);

        if (teamCapacity <= 0)
        {
            throw new TeamCapacityReachedException(team.TeamId, teamCapacity);
        }

        if (session.State is not (SessionState.Scheduled or SessionState.Preparing))
        {
            throw new LateJoinNotAllowedException(session.State);
        }

        if (team.JoinStatus != TeamJoinStatus.Open)
        {
            throw new TeamJoinClosedException(team.TeamId);
        }

        if (team.ActiveMemberCount >= teamCapacity)
        {
            throw new TeamCapacityReachedException(team.TeamId, teamCapacity);
        }
    }

    public void EnsureCanReconnect(LiveSession session, SessionParticipant participant, Team assignedTeam, Guid requestedTeamId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(assignedTeam);

        if (participant.IsRemoved)
        {
            throw new ParticipantRemovedFromSessionException(participant.SessionParticipantId);
        }

        if (!participant.IsDisconnected)
        {
            throw new ParticipantAlreadyConnectedException(participant.SessionParticipantId);
        }

        if (assignedTeam.TeamId != requestedTeamId)
        {
            throw new ParticipantAssignedToDifferentTeamException(
                participant.SessionParticipantId,
                assignedTeam.TeamId,
                requestedTeamId);
        }

        if (session.State is SessionState.Finished or SessionState.Cancelled)
        {
            throw new LateJoinNotAllowedException(session.State);
        }
    }
}
