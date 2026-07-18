using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Events;

public sealed class AccessDecisionRecordedEvent : BaseEvent
{
    public AccessDecisionRecordedEvent(User user, AccessDecision decision)
    {
        User = user;
        Decision = decision;
    }

    public User User { get; }

    public AccessDecision Decision { get; }
}
