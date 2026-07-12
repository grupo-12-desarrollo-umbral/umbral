using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.ValueObjects;

public sealed class ParticipantTeamBoardSnapshot : ValueObject
{
    private ParticipantTeamBoardSnapshot(
        Guid teamId,
        string teamDisplayName,
        string teamCode,
        int currentScore,
        AuthoritativeSessionTimerSnapshot timerSnapshot,
        ActiveSubstageContext? activeSubstageContext,
        IReadOnlyList<SubstageProgressItem> substages,
        IReadOnlyList<VisibleClue> visibleClues,
        IReadOnlyList<VisibleTarget> activeTargets)
    {
        TeamId = teamId;
        TeamDisplayName = teamDisplayName;
        TeamCode = teamCode;
        CurrentScore = currentScore;
        TimerSnapshot = timerSnapshot;
        ActiveSubstageContext = activeSubstageContext;
        Substages = substages;
        VisibleClues = visibleClues;
        ActiveTargets = activeTargets;
    }

    public Guid TeamId { get; }

    public string TeamDisplayName { get; }

    public string TeamCode { get; }

    public int CurrentScore { get; }

    public AuthoritativeSessionTimerSnapshot TimerSnapshot { get; }

    public ActiveSubstageContext? ActiveSubstageContext { get; }

    // The whole ordered substage sequence with per-item progress status (#171) — lets a
    // mixed-play-mode participant see where they are in the flow, not just the active substage.
    public IReadOnlyList<SubstageProgressItem> Substages { get; }

    public IReadOnlyList<VisibleClue> VisibleClues { get; }

    public IReadOnlyList<VisibleTarget> ActiveTargets { get; }

    public static ParticipantTeamBoardSnapshot Create(
        Guid teamId,
        string teamDisplayName,
        string teamCode,
        int currentScore,
        AuthoritativeSessionTimerSnapshot timerSnapshot,
        ActiveSubstageContext? activeSubstageContext,
        IReadOnlyList<SubstageProgressItem> substages,
        IReadOnlyList<VisibleClue> visibleClues,
        IReadOnlyList<VisibleTarget> activeTargets)
    {
        return new ParticipantTeamBoardSnapshot(
            teamId,
            teamDisplayName,
            teamCode,
            currentScore,
            timerSnapshot,
            activeSubstageContext,
            substages,
            visibleClues,
            activeTargets);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TeamId;
        yield return TeamDisplayName;
        yield return TeamCode;
        yield return CurrentScore;
        yield return TimerSnapshot;
        yield return ActiveSubstageContext;

        foreach (var substage in Substages)
        {
            yield return substage;
        }

        foreach (var clue in VisibleClues)
        {
            yield return clue;
        }

        foreach (var target in ActiveTargets)
        {
            yield return target;
        }
    }
}

// One entry in the ordered substage progress list (#171): its identity, display title, a
// session-wide display ordinal, its play mode, and where it sits relative to the active pointer.
public sealed record SubstageProgressItem(
    Guid SubstageSnapshotId,
    string Title,
    int SequenceOrder,
    SubstagePlayMode PlayMode,
    SubstageProgressStatus Status);

public sealed class ActiveSubstageContext : ValueObject
{
    private ActiveSubstageContext(
        Guid substageSnapshotId,
        SubstagePlayMode playMode,
        string title,
        int totalActiveTargets,
        int resolvedTargets,
        int? activeQuestionSequenceOrder,
        int? activeQuestionTimeLimitSeconds)
    {
        SubstageSnapshotId = substageSnapshotId;
        PlayMode = playMode;
        Title = title;
        TotalActiveTargets = totalActiveTargets;
        ResolvedTargets = resolvedTargets;
        ActiveQuestionSequenceOrder = activeQuestionSequenceOrder;
        ActiveQuestionTimeLimitSeconds = activeQuestionTimeLimitSeconds;
    }

    public Guid SubstageSnapshotId { get; }

    public SubstagePlayMode PlayMode { get; }

    public string Title { get; }

    public int TotalActiveTargets { get; }

    public int ResolvedTargets { get; }

    public int? ActiveQuestionSequenceOrder { get; }

    public int? ActiveQuestionTimeLimitSeconds { get; }

    public static ActiveSubstageContext CreateTreasureHunt(
        Guid substageSnapshotId,
        string title,
        int totalActiveTargets,
        int resolvedTargets)
    {
        return new ActiveSubstageContext(
            substageSnapshotId,
            SubstagePlayMode.TreasureHunt,
            title,
            totalActiveTargets,
            resolvedTargets,
            activeQuestionSequenceOrder: null,
            activeQuestionTimeLimitSeconds: null);
    }

    public static ActiveSubstageContext CreateTrivia(
        Guid substageSnapshotId,
        string title,
        int? activeQuestionSequenceOrder,
        int? activeQuestionTimeLimitSeconds)
    {
        return new ActiveSubstageContext(
            substageSnapshotId,
            SubstagePlayMode.Trivia,
            title,
            totalActiveTargets: 0,
            resolvedTargets: 0,
            activeQuestionSequenceOrder,
            activeQuestionTimeLimitSeconds);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SubstageSnapshotId;
        yield return PlayMode;
        yield return Title;
        yield return TotalActiveTargets;
        yield return ResolvedTargets;
        yield return ActiveQuestionSequenceOrder;
        yield return ActiveQuestionTimeLimitSeconds;
    }
}
