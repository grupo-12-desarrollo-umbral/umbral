namespace umbral_backend.Domain.Exceptions;

public sealed class MissionNotReadyForActivationException : DomainException
{
    public MissionNotReadyForActivationException(IReadOnlyCollection<string> readinessFailures)
        : base("Mission cannot be activated because its runtime plan is not ready: "
            + string.Join("; ", readinessFailures))
    {
        ReadinessFailures = readinessFailures;
    }

    public IReadOnlyCollection<string> ReadinessFailures { get; }

    public override ErrorCategory Category => ErrorCategory.Conflict;

    // Safe to expose: the message is composed only of curated, identifier-free readiness sentences
    // (the same strings the readiness endpoint returns), so the client sees why activation was blocked.
    public override string? PublicDetail => Message;
}
