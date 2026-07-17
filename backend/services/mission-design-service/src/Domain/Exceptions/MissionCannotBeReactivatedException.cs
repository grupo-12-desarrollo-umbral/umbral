namespace umbral_backend.Domain.Exceptions;

/// <summary>
/// Guards HU-09's terminal-retirement rule at the activation boundary: deactivation is permanent,
/// so a retired mission can never be reactivated — doing so would resurrect the record its past
/// sessions were built on. Distinct from <see cref="MissionAlreadyActiveException"/>, which rejects
/// re-activating an already-ready mission.
/// </summary>
public sealed class MissionCannotBeReactivatedException : DomainException
{
    public MissionCannotBeReactivatedException()
        : base("A deactivated mission cannot be reactivated.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;

    // Safe to expose: client-actionable and identifier-free.
    public override string? PublicDetail =>
        "This mission has been deactivated. Deactivation is permanent, so it cannot be reactivated.";
}
