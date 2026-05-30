using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class IdentityProviderSessionStartedEvent : BaseEvent
{
    public IdentityProviderSessionStartedEvent(IdentityProviderSession session)
    {
        Session = session;
    }

    public IdentityProviderSession Session { get; }
}
