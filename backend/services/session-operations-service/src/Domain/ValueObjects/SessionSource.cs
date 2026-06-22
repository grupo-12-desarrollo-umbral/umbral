using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

public sealed class SessionSource : ValueObject
{
    private SessionSource(SessionSourceType sourceType, Guid sourceEntityId)
    {
        SourceType = sourceType;
        SourceEntityId = sourceEntityId;
    }

    public SessionSourceType SourceType { get; }

    public Guid SourceEntityId { get; }

    public static SessionSource Create(Guid sourceMissionId)
    {
        if (sourceMissionId == Guid.Empty)
        {
            throw new SessionSourceEntityRequiredException();
        }

        return new SessionSource(SessionSourceType.Mission, sourceMissionId);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SourceType;
        yield return SourceEntityId;
    }
}
