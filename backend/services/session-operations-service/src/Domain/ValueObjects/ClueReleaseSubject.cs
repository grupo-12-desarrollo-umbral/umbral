using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

// The exactly-one-of subject of a manual clue release. Treasure-hunt clues remain keyed by their
// owning target snapshot; target-less trivia clues are keyed by their clue snapshot.
public sealed class ClueReleaseSubject : ValueObject
{
    private ClueReleaseSubject(Guid? targetId, Guid? clueId)
    {
        var hasTarget = targetId.HasValue;
        var hasClue = clueId.HasValue;
        if (hasTarget == hasClue || targetId == Guid.Empty || clueId == Guid.Empty)
        {
            throw new ClueReleaseSubjectInvalidException();
        }

        TargetId = targetId;
        ClueId = clueId;
    }

    public Guid? TargetId { get; }

    public Guid? ClueId { get; }

    public static ClueReleaseSubject Create(Guid? targetId, Guid? clueId)
    {
        return new ClueReleaseSubject(targetId, clueId);
    }

    public static ClueReleaseSubject ForTarget(Guid targetId)
    {
        return Create(targetId, null);
    }

    public static ClueReleaseSubject ForSubstageClue(Guid clueId)
    {
        return Create(null, clueId);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TargetId;
        yield return ClueId;
    }
}
