using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.Common.Exceptions;

public sealed class UserNotParticipantRoleException : Exception, IErrorMetadata
{
    public UserNotParticipantRoleException(int userId, Role actualRole)
        : base($"User '{userId}' must have role '{Role.Participant}' but has '{actualRole}'.")
    {
        UserId = userId;
        ActualRole = actualRole;
    }

    public int UserId { get; }

    public Role ActualRole { get; }

    public ErrorCategory Category => ErrorCategory.Unprocessable;

    public string ErrorCode => "user-not-participant-role";
}
