namespace umbral_backend.Domain.Exceptions;

/// <summary>
/// Guards HU-09's terminal-retirement rule: deactivation preserves a mission's usage history
/// (HU-09.3) and bars it from sourcing new sessions (HU-09.4), so a retired mission must stay
/// frozen as the record the sessions built from it were based on.
/// </summary>
public sealed class MissionNotEditableWhileInactiveException : DomainException
{
    public MissionNotEditableWhileInactiveException()
        : base("Mission cannot be edited while it is deactivated.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;

    // Safe to expose: client-actionable and identifier-free.
    public override string? PublicDetail =>
        "A deactivated mission cannot be edited. Deactivation is permanent, and the mission's "
        + "details are preserved as the record its past sessions were built on.";
}
