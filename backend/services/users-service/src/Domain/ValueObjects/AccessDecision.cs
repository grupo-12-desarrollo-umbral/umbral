using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.ValueObjects;

public sealed class AccessDecision : ValueObject
{
    private AccessDecision(ProtectedCapability capability, bool isAllowed, string reason)
    {
        Capability = capability;
        IsAllowed = isAllowed;
        Reason = reason;
    }

    public ProtectedCapability Capability { get; }

    public bool IsAllowed { get; }

    public string Reason { get; }

    public static AccessDecision Allow(ProtectedCapability capability, string reason)
    {
        return new AccessDecision(capability, true, reason);
    }

    public static AccessDecision Deny(ProtectedCapability capability, string reason)
    {
        return new AccessDecision(capability, false, reason);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Capability;
        yield return IsAllowed;
        yield return Reason;
    }
}
