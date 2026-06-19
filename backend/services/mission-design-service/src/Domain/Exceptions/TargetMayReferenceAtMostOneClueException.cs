namespace umbral_backend.Domain.Exceptions;

public sealed class TargetMayReferenceAtMostOneClueException : Exception
{
    public TargetMayReferenceAtMostOneClueException()
        : base("A target may reference at most one clue.")
    {
    }
}
