using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class UserAccessDeactivatedEvent : BaseEvent
{
    public UserAccessDeactivatedEvent(User user)
    {
        User = user;
    }

    public User User { get; }
}
