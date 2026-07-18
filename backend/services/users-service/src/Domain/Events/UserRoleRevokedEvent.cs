using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Events;

public sealed class UserRoleRevokedEvent : BaseEvent
{
    public UserRoleRevokedEvent(User user, Role revokedRole, Role currentRole)
    {
        User = user;
        RevokedRole = revokedRole;
        CurrentRole = currentRole;
    }

    public User User { get; }

    public Role RevokedRole { get; }

    public Role CurrentRole { get; }
}
