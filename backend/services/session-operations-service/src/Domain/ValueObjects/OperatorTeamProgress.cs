namespace umbral_backend.Domain.ValueObjects;

public sealed class OperatorTeamProgress : ValueObject
{
    private OperatorTeamProgress(
        Guid teamId,
        Guid? referenceTeamId,
        string teamCode,
        string displayName,
        int currentScore,
        int releasedClueCount,
        ActiveSubstageContext? activeSubstageContext)
    {
        TeamId = teamId;
        ReferenceTeamId = referenceTeamId;
        TeamCode = teamCode;
        DisplayName = displayName;
        CurrentScore = currentScore;
        ReleasedClueCount = releasedClueCount;
        ActiveSubstageContext = activeSubstageContext;
    }

    public Guid TeamId { get; }

    // The cross-context reference/catalog team id. Distinct from the runtime TeamId: scoring/ranking
    // (penalties, grants) key on this, so the operator surface must expose it to drive those actions.
    public Guid? ReferenceTeamId { get; }

    public string TeamCode { get; }

    public string DisplayName { get; }

    public int CurrentScore { get; }

    // Clues the team can currently see: operator-released manual clues plus the active substage's
    // always-on VisibleWhenSubstageStarts initial clues. A count only — no ranking/ledger.
    public int ReleasedClueCount { get; }

    public ActiveSubstageContext? ActiveSubstageContext { get; }

    public static OperatorTeamProgress Create(
        Guid teamId,
        Guid? referenceTeamId,
        string teamCode,
        string displayName,
        int currentScore,
        int releasedClueCount,
        ActiveSubstageContext? activeSubstageContext)
    {
        return new OperatorTeamProgress(teamId, referenceTeamId, teamCode, displayName, currentScore, releasedClueCount, activeSubstageContext);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TeamId;
        yield return ReferenceTeamId;
        yield return TeamCode;
        yield return DisplayName;
        yield return CurrentScore;
        yield return ReleasedClueCount;
        yield return ActiveSubstageContext;
    }
}
