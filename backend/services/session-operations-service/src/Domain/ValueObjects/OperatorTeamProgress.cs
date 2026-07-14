namespace umbral_backend.Domain.ValueObjects;

public sealed class OperatorTeamProgress : ValueObject
{
    private OperatorTeamProgress(
        Guid teamId,
        string teamCode,
        string displayName,
        int currentScore,
        int releasedClueCount,
        ActiveSubstageContext? activeSubstageContext)
    {
        TeamId = teamId;
        TeamCode = teamCode;
        DisplayName = displayName;
        CurrentScore = currentScore;
        ReleasedClueCount = releasedClueCount;
        ActiveSubstageContext = activeSubstageContext;
    }

    public Guid TeamId { get; }

    public string TeamCode { get; }

    public string DisplayName { get; }

    public int CurrentScore { get; }

    // Clues the team can currently see: operator-released manual clues plus the active substage's
    // always-on VisibleWhenSubstageStarts initial clues. A count only — no ranking/ledger.
    public int ReleasedClueCount { get; }

    public ActiveSubstageContext? ActiveSubstageContext { get; }

    public static OperatorTeamProgress Create(
        Guid teamId,
        string teamCode,
        string displayName,
        int currentScore,
        int releasedClueCount,
        ActiveSubstageContext? activeSubstageContext)
    {
        return new OperatorTeamProgress(teamId, teamCode, displayName, currentScore, releasedClueCount, activeSubstageContext);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TeamId;
        yield return TeamCode;
        yield return DisplayName;
        yield return CurrentScore;
        yield return ReleasedClueCount;
        yield return ActiveSubstageContext;
    }
}
