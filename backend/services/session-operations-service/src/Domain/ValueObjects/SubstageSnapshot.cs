using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

public sealed class SubstageSnapshot : ValueObject
{
    private SubstageSnapshot()
    {
        SubstageSnapshotId = Guid.Empty;
        Title = string.Empty;
    }

    private SubstageSnapshot(
        Guid substageSnapshotId,
        string title,
        int sequenceOrder,
        SubstagePlayMode playMode,
        int? winnerScore)
    {
        if (substageSnapshotId == Guid.Empty)
        {
            throw new SubstageSnapshotIdRequiredException();
        }

        if (playMode == SubstagePlayMode.Trivia && winnerScore.HasValue)
        {
            throw new TriviaSubstageSnapshotCannotDeclareWinnerScoreException();
        }

        SubstageSnapshotId = substageSnapshotId;
        Title = title.Trim();
        SequenceOrder = sequenceOrder;
        PlayMode = playMode;
        WinnerScore = winnerScore;
    }

    public Guid SubstageSnapshotId { get; }

    public string Title { get; }

    public int SequenceOrder { get; }

    public SubstagePlayMode PlayMode { get; }

    public int? WinnerScore { get; }

    public static SubstageSnapshot CreateTreasureHunt(string title, int sequenceOrder, int winnerScore)
    {
        return new SubstageSnapshot(Guid.NewGuid(), title, sequenceOrder, SubstagePlayMode.TreasureHunt, winnerScore);
    }

    public static SubstageSnapshot CreateTrivia(string title, int sequenceOrder)
    {
        return new SubstageSnapshot(Guid.NewGuid(), title, sequenceOrder, SubstagePlayMode.Trivia, winnerScore: null);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SubstageSnapshotId;
        yield return Title;
        yield return SequenceOrder;
        yield return PlayMode;
        yield return WinnerScore;
    }
}
