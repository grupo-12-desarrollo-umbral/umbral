using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class UnsupportedScoreSourceTypeException : DomainException
{
    public UnsupportedScoreSourceTypeException(ScoreSourceType sourceType)
        : base($"No score policy is registered for source type '{sourceType}'.")
    {
        SourceType = sourceType;
    }

    public ScoreSourceType SourceType { get; }

    public override ErrorCategory Category => ErrorCategory.Unprocessable;
}
