using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

public sealed class SessionSource : ValueObject
{
    private SessionSource(SessionSourceType sourceType, Guid sourceEntityId, int? sourceTriviaQuizId)
    {
        SourceType = sourceType;
        SourceEntityId = sourceEntityId;
        SourceTriviaQuizId = sourceTriviaQuizId;
    }

    public SessionSourceType SourceType { get; }

    public Guid SourceEntityId { get; }

    public int? SourceTriviaQuizId { get; }

    public static SessionSource Create(SessionSourceType sourceType, Guid sourceEntityId)
    {
        if (sourceType == SessionSourceType.TriviaQuiz)
        {
            throw new SessionSourceTriviaQuizIdRequiredException();
        }

        if (sourceEntityId == Guid.Empty)
        {
            throw new SessionSourceEntityRequiredException();
        }

        return new SessionSource(sourceType, sourceEntityId, sourceTriviaQuizId: null);
    }

    public static SessionSource CreateTriviaQuiz(int triviaQuizId)
    {
        if (triviaQuizId <= 0)
        {
            throw new SessionSourceTriviaQuizIdRequiredException();
        }

        return new SessionSource(SessionSourceType.TriviaQuiz, Guid.Empty, triviaQuizId);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SourceType;
        yield return SourceEntityId;
        yield return SourceTriviaQuizId;
    }
}
