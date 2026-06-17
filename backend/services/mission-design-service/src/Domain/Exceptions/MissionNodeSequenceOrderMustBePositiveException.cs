namespace umbral_backend.Domain.Exceptions;

public sealed class MissionNodeSequenceOrderMustBePositiveException : Exception
{
    public MissionNodeSequenceOrderMustBePositiveException()
        : base("Mission node sequence order must be a positive number.")
    {
    }
}
