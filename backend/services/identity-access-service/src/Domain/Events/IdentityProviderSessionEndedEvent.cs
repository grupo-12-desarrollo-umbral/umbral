using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class IdentityProviderSessionEndedEvent : BaseEvent
{
    public IdentityProviderSessionEndedEvent(IdentityProviderSession session)
    {
        Session = session;
    }

    public IdentityProviderSession Session { get; }
}
