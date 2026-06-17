namespace umbral_backend.Domain.Exceptions;

public sealed class ClueMustBelongToSameSubstageException : Exception
{
    public ClueMustBelongToSameSubstageException()
        : base("A target may only reference a clue from its own substage.")
    {
    }
}
