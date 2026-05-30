using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class UserProvisionedEvent : BaseEvent
{
    public UserProvisionedEvent(User user)
    {
        User = user;
    }

    public User User { get; }
}
