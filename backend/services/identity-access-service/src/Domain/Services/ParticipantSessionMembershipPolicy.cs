using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Services;

public sealed class ParticipantSessionMembershipPolicy
{
    public void EnsureCanSelfAssign(Guid requestedTeamId, int userId, Guid? existingTeamId)
    {
        if (existingTeamId.HasValue && existingTeamId.Value != requestedTeamId)
        {
            throw new ParticipantLockedToAnotherSessionTeamException(
                userId,
                existingTeamId.Value,
                requestedTeamId);
        }
    }
}
