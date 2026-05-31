using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Common.Exceptions;

public sealed class UserNotParticipantRoleException : Exception
{
    public UserNotParticipantRoleException(int userId, Role actualRole)
        : base($"User '{userId}' must have role '{Role.Participant}' but has '{actualRole}'.")
    {
        UserId = userId;
        ActualRole = actualRole;
    }

    public int UserId { get; }

    public Role ActualRole { get; }
}
