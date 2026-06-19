namespace umbral_backend.Domain.Exceptions;

public sealed class MissionNotReadyForActivationException : Exception
{
    public MissionNotReadyForActivationException(IReadOnlyCollection<string> readinessFailures)
        : base("Mission cannot be activated because its runtime plan is not ready: "
            + string.Join("; ", readinessFailures))
    {
        ReadinessFailures = readinessFailures;
    }

    public IReadOnlyCollection<string> ReadinessFailures { get; }
}
