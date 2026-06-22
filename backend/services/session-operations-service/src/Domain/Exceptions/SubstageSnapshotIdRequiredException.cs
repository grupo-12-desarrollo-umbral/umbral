namespace umbral_backend.Domain.Exceptions;

public sealed class SubstageSnapshotIdRequiredException : DomainException
{
    public SubstageSnapshotIdRequiredException()
        : base("Substage snapshot id is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
