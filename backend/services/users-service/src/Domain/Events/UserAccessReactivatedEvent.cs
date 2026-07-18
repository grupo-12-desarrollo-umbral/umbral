using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class UserAccessReactivatedEvent : BaseEvent
{
    public UserAccessReactivatedEvent(User user)
    {
        User = user;
    }

    public User User { get; }
}
