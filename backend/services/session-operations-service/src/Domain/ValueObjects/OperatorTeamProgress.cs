namespace umbral_backend.Domain.ValueObjects;

public sealed class OperatorTeamProgress : ValueObject
{
    private OperatorTeamProgress(
        Guid teamId,
        string teamCode,
        string displayName,
        int currentScore,
        ActiveSubstageContext? activeSubstageContext)
    {
        TeamId = teamId;
        TeamCode = teamCode;
        DisplayName = displayName;
        CurrentScore = currentScore;
        ActiveSubstageContext = activeSubstageContext;
    }

    public Guid TeamId { get; }

    public string TeamCode { get; }

    public string DisplayName { get; }

    public int CurrentScore { get; }

    public ActiveSubstageContext? ActiveSubstageContext { get; }

    public static OperatorTeamProgress Create(
        Guid teamId,
        string teamCode,
        string displayName,
        int currentScore,
        ActiveSubstageContext? activeSubstageContext)
    {
        return new OperatorTeamProgress(teamId, teamCode, displayName, currentScore, activeSubstageContext);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TeamId;
        yield return TeamCode;
        yield return DisplayName;
        yield return CurrentScore;
        yield return ActiveSubstageContext;
    }
}
