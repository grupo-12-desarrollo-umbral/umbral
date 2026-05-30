using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Events;

public sealed class UserRoleAssignedEvent : BaseEvent
{
    public UserRoleAssignedEvent(User user, Role previousRole, Role currentRole)
    {
        User = user;
        PreviousRole = previousRole;
        CurrentRole = currentRole;
    }

    public User User { get; }

    public Role PreviousRole { get; }

    public Role CurrentRole { get; }
}
