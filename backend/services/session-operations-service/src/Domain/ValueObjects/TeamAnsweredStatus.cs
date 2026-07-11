namespace umbral_backend.Domain.ValueObjects;

// Per-team cell of the pre-close restricted trivia monitor (HU-36A): the team's identity plus whether
// it answered the active synchronized question and when. It DELIBERATELY carries no
// SelectedOptionSequenceOrder, IsCorrect, or ScoreValue — the no-leak invariant is enforced by the
// type's shape, so the chosen option can never be revealed before the question closes.
public sealed class TeamAnsweredStatus : ValueObject
{
    private TeamAnsweredStatus(Guid teamId, string teamCode, string displayName, bool answered, DateTimeOffset? answeredAt)
    {
        TeamId = teamId;
        TeamCode = teamCode;
        DisplayName = displayName;
        Answered = answered;
        AnsweredAt = answeredAt;
    }

    public Guid TeamId { get; }

    public string TeamCode { get; }

    public string DisplayName { get; }

    public bool Answered { get; }

    // Set only when Answered — the accepted submission's SubmittedAt; null while the team has not answered.
    public DateTimeOffset? AnsweredAt { get; }

    public static TeamAnsweredStatus CreateAnswered(Guid teamId, string teamCode, string displayName, DateTimeOffset answeredAt)
    {
        return new TeamAnsweredStatus(teamId, teamCode, displayName, answered: true, answeredAt);
    }

    public static TeamAnsweredStatus CreateNotAnswered(Guid teamId, string teamCode, string displayName)
    {
        return new TeamAnsweredStatus(teamId, teamCode, displayName, answered: false, answeredAt: null);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TeamId;
        yield return TeamCode;
        yield return DisplayName;
        yield return Answered;
        yield return AnsweredAt;
    }
}
